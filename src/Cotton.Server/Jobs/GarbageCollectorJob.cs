using Cotton.Database;
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

            // 1. Remove orphaned file manifests (no associated NodeFiles)
            var manifestIds = await _dbContext.FileManifests
                .Where(fm => !fm.NodeFiles.Any())
                .OrderBy(fm => fm.Id)
                .Select(fm => fm.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (manifestIds.Count != 0)
            {
                await _dbContext.DownloadTokens
                    .Where(dt => manifestIds.Contains(dt.FileManifestId))
                    .ExecuteDeleteAsync(ct);

                await _dbContext.FileManifestChunks
                    .Where(m => manifestIds.Contains(m.FileManifestId))
                    .ExecuteDeleteAsync(ct);

                int deletedManifests = await _dbContext.FileManifests
                    .Where(fm => manifestIds.Contains(fm.Id))
                    .ExecuteDeleteAsync(ct);

                _logger.LogInformation("Removed {Count} orphaned file manifests.", deletedManifests);
            }

            // 2. Schedule orphaned chunks (no associated FileManifestChunks) for deletion
            DateTime deleteAfter = now.AddDays(ChunkGcDelayDays);
            int orphanedChunks = await _dbContext.Chunks
                .Where(c => !c.FileManifestChunks.Any() && c.GCScheduledAfter == null)
                .Take(BatchSize)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, deleteAfter), ct);
            if (orphanedChunks != 0)
            {
                _logger.LogInformation("Scheduled {Count} orphaned chunks for garbage collection.", orphanedChunks);
            }

            // 3. Delete chunks scheduled for deletion
            var chunksToDelete = await _dbContext.Chunks
                .Where(c => c.GCScheduledAfter != null && c.GCScheduledAfter <= now && !c.FileManifestChunks.Any())
                .Take(BatchSize)
                .ToListAsync(ct);
            int deletedChunksCounter = 0;
            if (chunksToDelete.Count != 0)
            {
                foreach (var chunkToDelete in chunksToDelete)
                {
                    string uid = Hasher.ToHexStringHash(chunkToDelete.Hash);
                    CurrentlyDeletingChunks.TryAdd(uid, 0);
                }

                _logger.LogInformation("Chunks retention will start in 1 minute. {Count} chunks scheduled for deletion.", chunksToDelete.Count);
                try
                {
                    await Task.Delay(60_000, ct);
                    foreach (var chunk in chunksToDelete)
                    {
                        var current = await _dbContext.Chunks
                            .AsNoTracking()
                            .Where(c => c.Hash == chunk.Hash)
                            .Select(c => new { c.GCScheduledAfter })
                            .SingleOrDefaultAsync(ct);
                        if (current == null || current.GCScheduledAfter == null || current.GCScheduledAfter > now)
                        {
                            continue;
                        }

                        bool stillOrphaned = !await _dbContext.FileManifestChunks.AnyAsync(m => m.ChunkHash == chunk.Hash, ct);
                        if (!stillOrphaned)
                        {
                            var tracked = await _dbContext.Chunks.FindAsync(chunk.Hash);
                            tracked?.GCScheduledAfter = null;
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
                            _logger.LogWarning("Failed to delete chunk {ChunkId} from storage, possibly already deleted.", uid);
                        }
                    }
                }
                finally
                {
                    CurrentlyDeletingChunks.Clear();
                }
                _logger.LogInformation("Garbage collection of chunks completed - {deletedChunksCounter} chunks deleted.", deletedChunksCounter);
            }
        }
    }
}
