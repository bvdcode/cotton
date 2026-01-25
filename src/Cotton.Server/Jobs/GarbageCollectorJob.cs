using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Collections.Concurrent;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class GarbageCollectorJob(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        SettingsProvider _settingsProvider,
        ILogger<GarbageCollectorJob> _logger) : IJob
    {
        private const int BatchSize = 10000;
        private const int ChunkGcDelayDays = 7;
        private static readonly ConcurrentDictionary<string, byte> CurrentlyDeletingChunks = new(comparer: StringComparer.OrdinalIgnoreCase);

        public static bool IsChunkBeingDeleted(string uid) => CurrentlyDeletingChunks.ContainsKey(uid);

        public async Task Execute(IJobExecutionContext context)
        {
            DateTime now = DateTime.UtcNow;
            CancellationToken ct = context.CancellationToken;

            await DeleteOrphanedManifestsAsync(ct);
            await ScheduleOrphanedChunksAsync(now, ct);
            await DeleteScheduledChunksAsync(now, ct);
        }

        private async Task DeleteOrphanedManifestsAsync(CancellationToken ct)
        {
            var manifestIds = await _dbContext.FileManifests
                .Where(fm => !fm.NodeFiles.Any())
                .OrderBy(fm => fm.Id)
                .Select(fm => fm.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (manifestIds.Count == 0)
            {
                return;
            }

            await _dbContext.DownloadTokens
                .Where(dt => manifestIds.Contains(dt.FileManifestId))
                .ExecuteDeleteAsync(ct);

            await _dbContext.FileManifestChunks
                .Where(m => manifestIds.Contains(m.FileManifestId))
                .ExecuteDeleteAsync(ct);

            int deletedManifests = await _dbContext.FileManifests
                .Where(fm => manifestIds.Contains(fm.Id))
                .ExecuteDeleteAsync(ct);

            if (deletedManifests != 0)
            {
                _logger.LogInformation("Removed {Count} orphaned file manifests.", deletedManifests);
            }
        }

        private async Task ScheduleOrphanedChunksAsync(DateTime now, CancellationToken ct)
        {
            StorageSpaceMode spaceMode = _settingsProvider.GetServerSettings().StorageSpaceMode;
            DateTime deleteAfter = spaceMode switch
            {
                StorageSpaceMode.Limited => now.AddDays(1),
                StorageSpaceMode.Unlimited => now.AddDays(ChunkGcDelayDays * 4),
                StorageSpaceMode.Optimal => now.AddDays(ChunkGcDelayDays),
                _ => now.AddDays(ChunkGcDelayDays),
            };

            int orphanedChunks = await _dbContext.Chunks
                .Where(c => !c.FileManifestChunks.Any() && c.GCScheduledAfter == null)
                .OrderBy(c => c.Hash)
                .Take(BatchSize)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, deleteAfter), ct);

            if (orphanedChunks != 0)
            {
                _logger.LogInformation("Scheduled {Count} orphaned chunks for garbage collection.", orphanedChunks);
            }
        }

        private async Task DeleteScheduledChunksAsync(DateTime now, CancellationToken ct)
        {
            var chunksToDelete = await _dbContext.Chunks
                .Where(c => c.GCScheduledAfter != null && c.GCScheduledAfter <= now && !c.FileManifestChunks.Any())
                .OrderBy(c => c.Hash)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (chunksToDelete.Count == 0)
            {
                return;
            }

            foreach (var chunkToDelete in chunksToDelete)
            {
                string uid = Hasher.ToHexStringHash(chunkToDelete.Hash);
                CurrentlyDeletingChunks.TryAdd(uid, 0);
            }

            int deletedChunksCounter = 0;
            try
            {
                _logger.LogInformation("{Count} chunks scheduled for deletion.", chunksToDelete.Count);
                await Task.Delay(5_000, ct);

                foreach (var chunk in chunksToDelete)
                {
                    if (!await IsStillEligibleForDeletionAsync(chunk.Hash, now, ct))
                    {
                        continue;
                    }

                    string uid = Hasher.ToHexStringHash(chunk.Hash);
                    await _dbContext.ChunkOwnerships
                        .Where(o => o.ChunkHash == chunk.Hash)
                        .ExecuteDeleteAsync(ct);

                    _dbContext.Chunks.Remove(chunk);
                    await _dbContext.SaveChangesAsync(ct);
                    deletedChunksCounter++;

                    bool deleted = await _storage.DeleteAsync(uid);
                    if (!deleted)
                    {
                        _logger.LogDebug("Chunk {ChunkId} storage delete returned false.", uid);
                    }
                }
            }
            finally
            {
                CurrentlyDeletingChunks.Clear();
            }

            _logger.LogInformation("Garbage collection completed - {Count} chunks deleted.", deletedChunksCounter);
        }

        private async Task<bool> IsStillEligibleForDeletionAsync(byte[] chunkHash, DateTime now, CancellationToken ct)
        {
            var current = await _dbContext.Chunks
                .AsNoTracking()
                .Where(c => c.Hash == chunkHash)
                .Select(c => new { c.GCScheduledAfter })
                .SingleOrDefaultAsync(ct);
            if (current == null || current.GCScheduledAfter == null || current.GCScheduledAfter > now)
            {
                return false;
            }

            bool stillOrphaned = !await _dbContext.FileManifestChunks.AnyAsync(m => m.ChunkHash == chunkHash, ct);
            if (!stillOrphaned)
            {
                var tracked = await _dbContext.Chunks.FindAsync([chunkHash], ct);
                tracked?.GCScheduledAfter = null;
                return false;
            }

            return true;
        }
    }
}
