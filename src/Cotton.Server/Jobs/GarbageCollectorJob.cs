using Cotton.Database;
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
        private static readonly ConcurrentDictionary<string, byte> CurrentlyDeletingChunks = new();
        private const int BatchSize = 10000;
        private const int ChunkGcDelayDays = 7;

        public static bool IsChunkBeingDeleted(string uid) => CurrentlyDeletingChunks.ContainsKey(uid);

        public async Task Execute(IJobExecutionContext context)
        {
            DateTime now = DateTime.UtcNow;

            // 1. Remove orphaned file manifests (no associated NodeFiles)
            var manifestIds = await _dbContext.FileManifests
                .Where(fm => !fm.NodeFiles.Any())
                .OrderBy(fm => fm.Id)
                .Select(fm => fm.Id)
                .Take(BatchSize)
                .ToListAsync(context.CancellationToken);

            if (manifestIds.Count != 0)
            {
                await _dbContext.FileManifestChunks
                    .Where(m => manifestIds.Contains(m.FileManifestId))
                    .ExecuteDeleteAsync(context.CancellationToken);

                int deletedManifests = await _dbContext.FileManifests
                    .Where(fm => manifestIds.Contains(fm.Id))
                    .ExecuteDeleteAsync(context.CancellationToken);

                _logger.LogInformation("Removed {Count} orphaned file manifests.", deletedManifests);
            }

            // 2. Schedule orphaned chunks (no associated FileManifestChunks) for deletion
            DateTime deleteAfter = now.AddDays(ChunkGcDelayDays);
            int orphanedChunks = await _dbContext.Chunks
                .Where(c => !c.FileManifestChunks.Any() && c.GCScheduledAfter == null)
                .Take(BatchSize)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, deleteAfter), context.CancellationToken);
            if (orphanedChunks != 0)
            {
                _logger.LogInformation("Scheduled {Count} orphaned chunks for garbage collection.", orphanedChunks);
            }

            // 3. Delete chunks scheduled for deletion
            var chunksToDelete = await _dbContext.Chunks
                .Where(c => c.GCScheduledAfter != null && c.GCScheduledAfter <= now)
                .Where(c => !c.FileManifestChunks.Any())
                .Take(BatchSize)
                .ToListAsync(context.CancellationToken);
            if (chunksToDelete.Count != 0)
            {
                foreach (var chunkToDelete in chunksToDelete)
                {
                    string uid = Convert.ToHexString(chunkToDelete.Hash);
                    CurrentlyDeletingChunks.TryAdd(uid, 0);
                }

                _logger.LogInformation("Chunks retention will start in 1 minute. {Count} chunks scheduled for deletion.", chunksToDelete.Count);
                await Task.Delay(60_000, context.CancellationToken);
                try
                {
                    foreach (var chunk in chunksToDelete)
                    {
                        bool stillOrphaned = !await _dbContext.FileManifestChunks.AnyAsync(m => m.ChunkHash == chunk.Hash);
                        if (!stillOrphaned)
                        {
                            chunk.GCScheduledAfter = null;
                            continue;
                        }

                        string uid = Convert.ToHexString(chunk.Hash);
                        bool deleted = await _storage.DeleteAsync(uid);
                        _dbContext.Chunks.Remove(chunk);
                        if (!deleted)
                        {
                            _logger.LogWarning("Failed to delete chunk {ChunkId} from storage, possibly already deleted.", uid);
                        }
                    }
                    await _dbContext.SaveChangesAsync(context.CancellationToken);
                }
                finally
                {
                    CurrentlyDeletingChunks.Clear();
                }
                _logger.LogInformation("Garbage collection of chunks completed - {Count} chunks deleted.", chunksToDelete.Count);
            }
        }
    }
}
