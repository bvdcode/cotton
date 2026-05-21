// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
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
    [JobTrigger(hours: 6)]
    [DisallowConcurrentExecution]
    public class GarbageCollectorJob(
        PerfTracker _perf,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ChunkUsageService _chunkUsage,
        SettingsProvider _settingsProvider,
        ILogger<GarbageCollectorJob> _logger) : IJob
    {
        private const int ChunkGcDelayDays = 7;
        private const int ManifestBatchSize = 1000;
        private const int MinChunkBatchSize = 1000;
        private const int MaxChunkBatchSize = 100000;
        private const int DeleteInnerBatchSize = 500;
        private const int ScheduleInnerBatchSize = 2000;
        private const int StorageDeleteConcurrency = 8;
        private static readonly TimeSpan ProgressLogInterval = TimeSpan.FromSeconds(15);
        private static readonly ConcurrentDictionary<string, byte> CurrentlyDeletingChunks = new(comparer: StringComparer.OrdinalIgnoreCase);

        public static bool IsChunkBeingDeleted(string uid) => CurrentlyDeletingChunks.ContainsKey(uid);

        private static bool _isFirstRun = true;

        public async Task Execute(IJobExecutionContext context)
        {
            bool isNightTime = _perf.IsNightTime();
            StorageSpaceMode spaceMode = _settingsProvider.GetServerSettings().StorageSpaceMode;
            bool isAggressiveMode = spaceMode == StorageSpaceMode.Limited;
            if (!isAggressiveMode && isNightTime)
            {
                _logger.LogInformation("Skipping garbage collection run because it's currently night time and aggressive GC mode is not enabled.");
                return;
            }

            if (_isFirstRun)
            {
                _isFirstRun = false;
                _logger.LogInformation("Waiting for 15 minutes before the first garbage collection run to allow the server to start up.");
                await Task.Delay(900_000, context.CancellationToken); // Wait for 15 minutes for the server to start up and stabilize
            }

            int batchSize = spaceMode switch
            {
                StorageSpaceMode.Limited => MaxChunkBatchSize,
                StorageSpaceMode.Unlimited => MinChunkBatchSize,
                StorageSpaceMode.Optimal => (MinChunkBatchSize + MaxChunkBatchSize) / 2,
                _ => MinChunkBatchSize * 2,
            };

            await RunOnceAsync(DateTime.UtcNow, batchSize, context.CancellationToken);
        }

        public async Task RunOnceAsync(DateTime now, int batchSize, CancellationToken ct = default)
        {
            HashSet<string> protectedStorageKeys = await _chunkUsage.GetProtectedStorageKeysAsync(ct);

            await DeleteOrphanedManifestsAsync(ct);
            await ClearSchedulesForReferencedChunksAsync(protectedStorageKeys, ct);
            await ScheduleOrphanedChunksAsync(now, protectedStorageKeys, batchSize, ct);
            await DeleteScheduledChunksAsync(now, batchSize, protectedStorageKeys, ct);
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

        private async Task ScheduleOrphanedChunksAsync(DateTime now, HashSet<string> protectedStorageKeys, int batchSize, CancellationToken ct)
        {
            StorageSpaceMode spaceMode = _settingsProvider.GetServerSettings().StorageSpaceMode;
            DateTime deleteAfter = spaceMode switch
            {
                StorageSpaceMode.Limited => now.AddDays(1),
                StorageSpaceMode.Unlimited => now.AddDays(ChunkGcDelayDays * 4),
                StorageSpaceMode.Optimal => now.AddDays(ChunkGcDelayDays),
                _ => now.AddDays(ChunkGcDelayDays),
            };

            int totalScheduled = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var lastProgressLogAt = TimeSpan.Zero;

            while (totalScheduled < batchSize)
            {
                int take = Math.Min(batchSize - totalScheduled, ScheduleInnerBatchSize);

                IQueryable<Chunk> baseQuery = _chunkUsage.WhereUnreferencedByDatabase(_dbContext.Chunks);
                IQueryable<Chunk> filteredQuery = _chunkUsage.WhereNotProtectedByStorageKeys(baseQuery, protectedStorageKeys);

                var candidateHashes = await filteredQuery
                    .AsNoTracking()
                    .Where(c => c.GCScheduledAfter == null)
                    .OrderBy(c => c.Hash)
                    .Take(take)
                    .Select(c => c.Hash)
                    .ToListAsync(ct);

                if (candidateHashes.Count == 0)
                {
                    break;
                }

                int updated = await _dbContext.Chunks
                    .Where(c => candidateHashes.Contains(c.Hash) && c.GCScheduledAfter == null)
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, deleteAfter), ct);

                totalScheduled += updated;

                if (stopwatch.Elapsed - lastProgressLogAt >= ProgressLogInterval && totalScheduled < batchSize)
                {
                    _logger.LogInformation(
                        "Garbage collection scheduling progress: {Scheduled} orphaned chunks scheduled so far.",
                        totalScheduled);
                    lastProgressLogAt = stopwatch.Elapsed;
                }

                if (candidateHashes.Count < take)
                {
                    break;
                }
            }

            if (totalScheduled != 0)
            {
                _logger.LogInformation("Scheduled {Count} orphaned chunks for garbage collection.", totalScheduled);
            }
        }

        private async Task DeleteScheduledChunksAsync(DateTime now, int batchSize, HashSet<string> protectedStorageKeys, CancellationToken ct)
        {
            var hashesToDelete = await _chunkUsage
                .WhereUnreferencedByDatabase(_dbContext.Chunks)
                .Where(c => c.GCScheduledAfter != null
                    && c.GCScheduledAfter <= now)
                .OrderBy(c => c.Hash)
                .Take(batchSize)
                .AsNoTracking()
                .Select(c => c.Hash)
                .ToListAsync(ct);

            if (hashesToDelete.Count == 0)
            {
                return;
            }

            List<byte[]> reservedHashes = new(hashesToDelete.Count);
            HashSet<string> deletingNow = new(StringComparer.OrdinalIgnoreCase);
            List<byte[]> protectedHashesToClear = [];

            foreach (byte[] hash in hashesToDelete)
            {
                string uid = Hasher.ToHexStringHash(hash);
                if (protectedStorageKeys.Contains(uid))
                {
                    protectedHashesToClear.Add(hash);
                    continue;
                }

                if (CurrentlyDeletingChunks.TryAdd(uid, 0))
                {
                    deletingNow.Add(uid);
                    reservedHashes.Add(hash);
                }
                else
                {
                    _logger.LogDebug("Chunk {ChunkId} is already being deleted by another GC run.", uid);
                }
            }

            if (protectedHashesToClear.Count > 0)
            {
                await _dbContext.Chunks
                    .Where(c => protectedHashesToClear.Contains(c.Hash) && c.GCScheduledAfter != null)
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), ct);
            }

            if (reservedHashes.Count == 0)
            {
                return;
            }

            int deletedChunksCounter = 0;
            int processedCounter = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var lastProgressLogAt = TimeSpan.Zero;

            try
            {
                _logger.LogInformation("{Count} chunks scheduled for deletion.", reservedHashes.Count);
                await Task.Delay(5_000, ct);

                for (int i = 0; i < reservedHashes.Count; i += DeleteInnerBatchSize)
                {
                    int end = Math.Min(i + DeleteInnerBatchSize, reservedHashes.Count);
                    List<byte[]> batchHashes = reservedHashes.GetRange(i, end - i);

                    try
                    {
                        int deletedInBatch = await DeleteEligibleBatchAsync(batchHashes, now, protectedStorageKeys, ct);
                        deletedChunksCounter += deletedInBatch;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete batch of {Count} scheduled chunks.", batchHashes.Count);
                    }

                    processedCounter = end;

                    if (stopwatch.Elapsed - lastProgressLogAt >= ProgressLogInterval && processedCounter < reservedHashes.Count)
                    {
                        double rate = processedCounter / Math.Max(1.0, stopwatch.Elapsed.TotalSeconds);
                        _logger.LogInformation(
                            "Garbage collection progress: {Processed}/{Total} chunks, {Deleted} deleted ({Rate:F0}/s).",
                            processedCounter,
                            reservedHashes.Count,
                            deletedChunksCounter,
                            rate);
                        lastProgressLogAt = stopwatch.Elapsed;
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

            _logger.LogInformation(
                "Garbage collection completed - {Count} chunks deleted in {Elapsed}.",
                deletedChunksCounter,
                stopwatch.Elapsed);
        }

        private async Task<int> DeleteEligibleBatchAsync(
            List<byte[]> batchHashes,
            DateTime now,
            HashSet<string> protectedStorageKeys,
            CancellationToken ct)
        {
            var stillScheduledHashes = await _dbContext.Chunks
                .AsNoTracking()
                .Where(c => batchHashes.Contains(c.Hash))
                .Where(c => c.GCScheduledAfter != null && c.GCScheduledAfter <= now)
                .Select(c => c.Hash)
                .ToListAsync(ct);

            if (stillScheduledHashes.Count == 0)
            {
                return 0;
            }

            var nowReferencedHashes = await _chunkUsage
                .WhereReferencedByDatabase(_dbContext.Chunks)
                .AsNoTracking()
                .Where(c => stillScheduledHashes.Contains(c.Hash))
                .Select(c => c.Hash)
                .ToListAsync(ct);

            if (nowReferencedHashes.Count > 0)
            {
                await _dbContext.Chunks
                    .Where(c => nowReferencedHashes.Contains(c.Hash) && c.GCScheduledAfter != null)
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), ct);
            }

            HashSet<string> referencedUids = nowReferencedHashes
                .Select(Hasher.ToHexStringHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<byte[]> eligibleHashes = [];
            foreach (byte[] hash in stillScheduledHashes)
            {
                string uid = Hasher.ToHexStringHash(hash);
                if (referencedUids.Contains(uid) || protectedStorageKeys.Contains(uid))
                {
                    continue;
                }

                eligibleHashes.Add(hash);
            }

            if (eligibleHashes.Count == 0)
            {
                return 0;
            }

            int dbDeleted;
            await using (var transaction = await _dbContext.Database.BeginTransactionAsync(ct))
            {
                try
                {
                    await _dbContext.ChunkOwnerships
                        .Where(o => eligibleHashes.Contains(o.ChunkHash))
                        .ExecuteDeleteAsync(ct);

                    dbDeleted = await _dbContext.Chunks
                        .Where(c => eligibleHashes.Contains(c.Hash))
                        .ExecuteDeleteAsync(ct);

                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            }

            await Parallel.ForEachAsync(
                eligibleHashes,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = StorageDeleteConcurrency,
                    CancellationToken = ct,
                },
                async (hash, _) =>
                {
                    string uid = Hasher.ToHexStringHash(hash);
                    try
                    {
                        bool deleted = await _storage.DeleteAsync(uid);
                        if (!deleted)
                        {
                            _logger.LogDebug("Chunk {ChunkId} storage delete returned false.", uid);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete chunk {ChunkId} from storage.", uid);
                    }
                });

            return dbDeleted;
        }
    }
}
