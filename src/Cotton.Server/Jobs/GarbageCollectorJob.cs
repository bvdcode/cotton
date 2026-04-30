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
    [DisallowConcurrentExecution]
    public class GarbageCollectorJob(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ChunkUsageService _chunkUsage,
        SettingsProvider _settingsProvider,
        ILogger<GarbageCollectorJob> _logger) : IJob
    {
        private const int ManifestBatchSize = 1000;
        private const int ChunkBatchSize = 1000;
        private const int ChunkGcDelayDays = 7;
        private const int InitialDelayMs = 60_000;
        private static readonly ConcurrentDictionary<string, byte> CurrentlyDeletingChunks = new(comparer: StringComparer.OrdinalIgnoreCase);

        public static bool IsChunkBeingDeleted(string uid) => CurrentlyDeletingChunks.ContainsKey(uid);

        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(900_000, context.CancellationToken); // Wait for 15 minutes for the server to start up and stabilize

            _logger.LogInformation(
                "Waiting {InitialDelayMs} seconds before starting garbage collection to allow any ongoing operations to complete...",
                InitialDelayMs / 1000);
            await Task.Delay(InitialDelayMs, context.CancellationToken);

            await RunOnceAsync(DateTime.UtcNow, context.CancellationToken);
        }

        public async Task RunOnceAsync(DateTime now, CancellationToken ct = default)
        {
            HashSet<string> protectedStorageKeys = await _chunkUsage.GetProtectedStorageKeysAsync(ct);

            await DeleteOrphanedManifestsAsync(ct);
            await ClearSchedulesForReferencedChunksAsync(protectedStorageKeys, ct);
            await ScheduleOrphanedChunksAsync(now, protectedStorageKeys, ct);
            await DeleteScheduledChunksAsync(now, protectedStorageKeys, ct);
        }

        private async Task DeleteOrphanedManifestsAsync(CancellationToken ct)
        {
            var manifestIds = await _dbContext.FileManifests
                .Where(fm => !fm.NodeFiles.Any())
                .OrderBy(fm => fm.Id)
                .Select(fm => fm.Id)
                .Take(ManifestBatchSize)
                .ToListAsync(ct);

            if (manifestIds.Count == 0)
            {
                return;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await _dbContext.DownloadTokens
                    .Where(dt => manifestIds.Contains(dt.NodeFile.FileManifestId))
                    .ExecuteDeleteAsync(ct);

                await _dbContext.FileManifestChunks
                    .Where(m => manifestIds.Contains(m.FileManifestId))
                    .ExecuteDeleteAsync(ct);

                int deletedManifests = await _dbContext.FileManifests
                    .Where(fm => manifestIds.Contains(fm.Id) && !fm.NodeFiles.Any())
                    .ExecuteDeleteAsync(ct);

                if (deletedManifests != manifestIds.Count)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogWarning(
                        "Skipped orphaned file manifest cleanup because {ReferencedCount} of {TotalCount} candidates became referenced during garbage collection.",
                        manifestIds.Count - deletedManifests,
                        manifestIds.Count);
                    return;
                }

                await transaction.CommitAsync(ct);

                if (deletedManifests != 0)
                {
                    _logger.LogInformation("Removed {Count} orphaned file manifests.", deletedManifests);
                }
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogWarning(ex, "Skipped orphaned file manifest cleanup because a candidate became referenced during garbage collection.");
            }
        }

        private async Task ClearSchedulesForReferencedChunksAsync(HashSet<string> protectedStorageKeys, CancellationToken ct)
        {
            int referencedCleared = await _chunkUsage.ClearGcSchedulesForReferencedChunksAsync(ct);
            int protectedCleared = await _chunkUsage.ClearGcSchedulesForProtectedChunksAsync(protectedStorageKeys, ct);
            int clearedCount = referencedCleared + protectedCleared;

            if (clearedCount > 0)
            {
                _logger.LogInformation("Cleared garbage collection schedule for {Count} live chunks.", clearedCount);
            }
        }

        private async Task ScheduleOrphanedChunksAsync(DateTime now, HashSet<string> protectedStorageKeys, CancellationToken ct)
        {
            StorageSpaceMode spaceMode = _settingsProvider.GetServerSettings().StorageSpaceMode;
            DateTime deleteAfter = spaceMode switch
            {
                StorageSpaceMode.Limited => now.AddDays(1),
                StorageSpaceMode.Unlimited => now.AddDays(ChunkGcDelayDays * 4),
                StorageSpaceMode.Optimal => now.AddDays(ChunkGcDelayDays),
                _ => now.AddDays(ChunkGcDelayDays),
            };

            int scheduledCount = 0;
            int scannedCount = 0;
            var orphanedChunksQuery = _chunkUsage
                .WhereUnreferencedByDatabase(_dbContext.Chunks)
                .Where(c => c.GCScheduledAfter == null)
                .OrderBy(c => c.Hash);

            while (scheduledCount < ChunkBatchSize)
            {
                var orphanedChunks = await orphanedChunksQuery
                    .Skip(scannedCount)
                    .Take(ChunkBatchSize)
                    .ToListAsync(ct);

                if (orphanedChunks.Count == 0)
                {
                    break;
                }

                scannedCount += orphanedChunks.Count;
                foreach (var chunk in orphanedChunks)
                {
                    string uid = Hasher.ToHexStringHash(chunk.Hash);
                    if (protectedStorageKeys.Contains(uid))
                    {
                        continue;
                    }

                    chunk.GCScheduledAfter = deleteAfter;
                    scheduledCount++;

                    if (scheduledCount == ChunkBatchSize)
                    {
                        break;
                    }
                }

                if (orphanedChunks.Count < ChunkBatchSize)
                {
                    break;
                }
            }

            if (scheduledCount > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            if (scheduledCount != 0)
            {
                _logger.LogInformation("Scheduled {Count} orphaned chunks for garbage collection.", scheduledCount);
            }
        }

        private async Task DeleteScheduledChunksAsync(DateTime now, HashSet<string> protectedStorageKeys, CancellationToken ct)
        {
            var chunksToDelete = await _chunkUsage
                .WhereUnreferencedByDatabase(_dbContext.Chunks)
                .Where(c => c.GCScheduledAfter != null
                    && c.GCScheduledAfter <= now)
                .OrderBy(c => c.Hash)
                .Take(ChunkBatchSize)
                .ToListAsync(ct);

            if (chunksToDelete.Count == 0)
            {
                return;
            }

            HashSet<string> deletingNow = new(StringComparer.OrdinalIgnoreCase);
            foreach (var chunkToDelete in chunksToDelete)
            {
                string uid = Hasher.ToHexStringHash(chunkToDelete.Hash);
                if (protectedStorageKeys.Contains(uid))
                {
                    await _chunkUsage.ClearGcScheduleAsync(chunkToDelete.Hash, ct);
                    continue;
                }

                if (CurrentlyDeletingChunks.TryAdd(uid, 0))
                {
                    deletingNow.Add(uid);
                }
                else
                {
                    _logger.LogDebug("Chunk {ChunkId} is already being deleted by another GC run.", uid);
                }
            }

            if (deletingNow.Count == 0)
            {
                return;
            }

            int deletedChunksCounter = 0;
            try
            {
                _logger.LogInformation("{Count} chunks scheduled for deletion.", deletingNow.Count);
                await Task.Delay(5_000, ct);

                foreach (var chunk in chunksToDelete)
                {
                    string uid = Hasher.ToHexStringHash(chunk.Hash);
                    if (!deletingNow.Contains(uid) || protectedStorageKeys.Contains(uid))
                    {
                        continue;
                    }

                    try
                    {
                        if (!await IsStillEligibleForDeletionAsync(chunk.Hash, now, protectedStorageKeys, ct))
                        {
                            continue;
                        }

                        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                        try
                        {
                            await _dbContext.ChunkOwnerships
                                .Where(o => o.ChunkHash == chunk.Hash)
                                .ExecuteDeleteAsync(ct);

                            _dbContext.Chunks.Remove(chunk);
                            await _dbContext.SaveChangesAsync(ct);
                            await transaction.CommitAsync(ct);
                        }
                        catch
                        {
                            await transaction.RollbackAsync(ct);
                            throw;
                        }

                        deletedChunksCounter++;

                        bool deleted = await _storage.DeleteAsync(uid);
                        if (!deleted)
                        {
                            _logger.LogDebug("Chunk {ChunkId} storage delete returned false.", uid);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete scheduled chunk {ChunkId}.", uid);
                    }
                }
            }
            finally
            {
                foreach (string uid in deletingNow)
                {
                    CurrentlyDeletingChunks.TryRemove(uid, out _);
                }
            }

            _logger.LogInformation("Garbage collection completed - {Count} chunks deleted.", deletedChunksCounter);
        }

        private async Task<bool> IsStillEligibleForDeletionAsync(byte[] chunkHash, DateTime now, HashSet<string> protectedStorageKeys, CancellationToken ct)
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

            string uid = Hasher.ToHexStringHash(chunkHash);
            bool stillOrphaned = !await _chunkUsage.HasDatabaseReferencesAsync(chunkHash, ct)
                && !protectedStorageKeys.Contains(uid);
            if (!stillOrphaned)
            {
                await _chunkUsage.ClearGcScheduleAsync(chunkHash, ct);
                return false;
            }

            return true;
        }
    }
}
