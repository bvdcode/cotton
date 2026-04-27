using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Collections.Concurrent;

namespace Cotton.Server.Jobs
{
    // TODO: Refactor to use a more robust approach for scheduling and deleting
    // orphaned chunks, e.g. by using a separate table to track scheduled deletions
    // and a background worker that processes that table at a configurable interval.
    // The current approach has potential issues with concurrency and reliability,
    // e.g. if the job is triggered multiple times in quick succession or if the job
    // fails after scheduling chunks for deletion but before actually deleting them.
    // TODO: WHAT!? Who wrote this... This job was good, I have no idea what the comment above is about.

    // TODO: Don't forget to check:
    // 1. Files
    // 2. Small previews
    // 3. Large previews
    // 4. Database backups
    // 5. User avatars

    [JobTrigger(days: 1)]
    public class GarbageCollectorJob(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        IDatabaseBackupManifestService _backupManifestService,
        DatabaseBackupKeyProvider _backupKeyProvider,
        SettingsProvider _settingsProvider,
        ILogger<GarbageCollectorJob> _logger) : IJob
    {
        private readonly bool DryRunEnabled = true;
        private const int ManifestBatchSize = 1000;
        private const int ChunkBatchSize = 1000;
        private const int ChunkGcDelayDays = 7;
        private const int InitialDelayMs = 60_000;
        private static readonly ConcurrentDictionary<string, byte> CurrentlyDeletingChunks = new(comparer: StringComparer.OrdinalIgnoreCase);

        public static bool IsChunkBeingDeleted(string uid) => CurrentlyDeletingChunks.ContainsKey(uid);

        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(900_000); // Wait for 15 minutes for the server to start up and stabilize

            if (DryRunEnabled)
            {
                _logger.LogWarning("GarbageCollectorJob is running in DRY-RUN mode. No DB rows or storage objects will be deleted/updated.");
            }

            _logger.LogInformation(
                "Waiting {InitialDelayMs} seconds before starting garbage collection to allow any ongoing operations to complete...",
                InitialDelayMs / 1000);
            await Task.Delay(InitialDelayMs, context.CancellationToken);

            DateTime now = DateTime.UtcNow;
            CancellationToken ct = context.CancellationToken;
            HashSet<string> protectedStorageKeys = await GetProtectedStorageKeysAsync(ct);

            await DeleteOrphanedManifestsAsync(ct);
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

            if (DryRunEnabled)
            {
                _logger.LogInformation("[DRY-RUN] Would remove {Count} orphaned file manifests.", manifestIds.Count);
                return;
            }

            await _dbContext.DownloadTokens
                .Where(dt => manifestIds.Contains(dt.NodeFile.FileManifestId))
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

            var orphanedChunks = await _dbContext.Chunks
                .Where(c => !c.FileManifestChunks.Any()
                    && !_dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash || fm.LargeFilePreviewHash == c.Hash)
                    && !_dbContext.Users.Any(u => u.AvatarHash == c.Hash)
                    && c.GCScheduledAfter == null)
                .OrderBy(c => c.Hash)
                .Take(ChunkBatchSize)
                .ToListAsync(ct);

            int scheduledCount = 0;
            foreach (var chunk in orphanedChunks)
            {
                string uid = Hasher.ToHexStringHash(chunk.Hash);
                if (protectedStorageKeys.Contains(uid))
                {
                    continue;
                }

                chunk.GCScheduledAfter = deleteAfter;
                scheduledCount++;
            }

            if (DryRunEnabled)
            {
                if (scheduledCount > 0)
                {
                    _logger.LogInformation("[DRY-RUN] Would schedule {Count} orphaned chunks for garbage collection.", scheduledCount);
                }
                return;
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
            var chunksToDelete = await _dbContext.Chunks
                .Where(c => c.GCScheduledAfter != null
                    && c.GCScheduledAfter <= now
                    && !c.FileManifestChunks.Any()
                    && !_dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash || fm.LargeFilePreviewHash == c.Hash)
                    && !_dbContext.Users.Any(u => u.AvatarHash == c.Hash))
                .OrderBy(c => c.Hash)
                .Take(ChunkBatchSize)
                .ToListAsync(ct);

            if (chunksToDelete.Count == 0)
            {
                return;
            }

            if (DryRunEnabled)
            {
                int deletableCount = chunksToDelete.Count(c => !protectedStorageKeys.Contains(Hasher.ToHexStringHash(c.Hash)));
                _logger.LogInformation("[DRY-RUN] Would delete up to {Count} scheduled chunks in this run.", deletableCount);
                return;
            }

            List<string> deletingNow = [];
            foreach (var chunkToDelete in chunksToDelete)
            {
                string uid = Hasher.ToHexStringHash(chunkToDelete.Hash);
                if (protectedStorageKeys.Contains(uid))
                {
                    await ClearGcScheduleAsync(chunkToDelete.Hash, ct);
                    continue;
                }

                CurrentlyDeletingChunks.TryAdd(uid, 0);
                deletingNow.Add(uid);
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
                    if (protectedStorageKeys.Contains(uid))
                    {
                        continue;
                    }

                    try
                    {
                        if (!await IsStillEligibleForDeletionAsync(chunk.Hash, now, protectedStorageKeys, ct))
                        {
                            continue;
                        }

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
            bool stillOrphaned = !await _dbContext.FileManifestChunks.AnyAsync(m => m.ChunkHash == chunkHash, ct)
                && !await _dbContext.FileManifests.AnyAsync(fm => fm.SmallFilePreviewHash == chunkHash || fm.LargeFilePreviewHash == chunkHash, ct)
                && !await _dbContext.Users.AnyAsync(u => u.AvatarHash == chunkHash, ct)
                && !protectedStorageKeys.Contains(uid);
            if (!stillOrphaned)
            {
                await ClearGcScheduleAsync(chunkHash, ct);
                return false;
            }

            return true;
        }

        private async Task ClearGcScheduleAsync(byte[] chunkHash, CancellationToken ct)
        {
            await _dbContext.Chunks
                .Where(c => c.Hash == chunkHash)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), ct);
        }

        private async Task<HashSet<string>> GetProtectedStorageKeysAsync(CancellationToken ct)
        {
            HashSet<string> protectedStorageKeys = new(StringComparer.OrdinalIgnoreCase)
            {
                _backupKeyProvider.GetScopedPointerStorageKey()
            };

            var latestBackup = await _backupManifestService.TryGetLatestManifestAsync(ct);
            if (latestBackup is null)
            {
                return protectedStorageKeys;
            }

            protectedStorageKeys.Add(latestBackup.ManifestStorageKey);
            foreach (var chunk in latestBackup.Manifest.Chunks)
            {
                if (!string.IsNullOrWhiteSpace(chunk.StorageKey))
                {
                    protectedStorageKeys.Add(chunk.StorageKey);
                }
            }

            return protectedStorageKeys;
        }
    }
}
