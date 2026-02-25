using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Processors;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 30)]
    public class StorageConsistencyJob(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        INotificationsProvider _notifications,
        ILogger<StorageConsistencyJob> _logger) : IJob
    {
        private const int BatchSize = 10000;

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken ct = context.CancellationToken;

            _logger.LogInformation("Storage consistency check started.");

            HashSet<string> storageKeys = await CollectStorageKeysAsync(ct);
            _logger.LogInformation("Found {Count} keys in storage.", storageKeys.Count);

            await CheckDbChunksAgainstStorageAsync(storageKeys, ct);
            await RegisterOrphanedStorageKeysAsync(storageKeys, ct);

            _logger.LogInformation("Storage consistency check completed.");
        }

        private async Task<HashSet<string>> CollectStorageKeysAsync(CancellationToken ct)
        {
            HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
            await foreach (string key in _storage.ListAllKeysAsync(ct))
            {
                keys.Add(key);
            }
            return keys;
        }

        private async Task CheckDbChunksAgainstStorageAsync(HashSet<string> storageKeys, CancellationToken ct)
        {
            int missingCount = 0;
            int offset = 0;

            while (true)
            {
                var batch = await _dbContext.Chunks
                    .OrderBy(c => c.Hash)
                    .Skip(offset)
                    .Take(BatchSize)
                    .Select(c => c.Hash)
                    .ToListAsync(ct);

                if (batch.Count == 0)
                {
                    break;
                }

                foreach (byte[] chunkHash in batch)
                {
                    string uid = Hasher.ToHexStringHash(chunkHash);

                    if (storageKeys.Remove(uid))
                    {
                        continue;
                    }

                    _logger.LogWarning("Chunk {Uid} exists in DB but missing from storage.", uid);
                    await HandleMissingChunkAsync(chunkHash, ct);
                    missingCount++;
                }

                offset += batch.Count;
            }

            if (missingCount > 0)
            {
                _logger.LogError("Storage consistency check found {Count} chunks missing from storage.", missingCount);
            }
        }

        private async Task HandleMissingChunkAsync(byte[] chunkHash, CancellationToken ct)
        {
            var affectedManifestIds = await _dbContext.FileManifestChunks
                .Where(fmc => fmc.ChunkHash == chunkHash)
                .Select(fmc => fmc.FileManifestId)
                .Distinct()
                .ToListAsync(ct);

            var affectedPreviewManifestIds = await _dbContext.FileManifests
                .Where(fm => fm.SmallFilePreviewHash == chunkHash || fm.LargeFilePreviewHash == chunkHash)
                .Select(fm => fm.Id)
                .ToListAsync(ct);

            var allManifestIds = affectedManifestIds.Union(affectedPreviewManifestIds).Distinct().ToList();

            var affectedNodeFiles = await _dbContext.NodeFiles
                .Where(nf => allManifestIds.Contains(nf.FileManifestId))
                .ToListAsync(ct);

            HashSet<(Guid OwnerId, string FileName)> notified = [];
            foreach (var nodeFile in affectedNodeFiles)
            {
                if (notified.Add((nodeFile.OwnerId, nodeFile.Name)))
                {
                    try
                    {
                        await _notifications.SendStorageChunkMissingNotificationAsync(
                            nodeFile.OwnerId,
                            nodeFile.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send missing chunk notification to user {UserId} for file {FileName}.",
                            nodeFile.OwnerId, nodeFile.Name);
                    }
                }
            }

            // Clear preview references pointing to this missing chunk
            await _dbContext.FileManifests
                .Where(fm => fm.SmallFilePreviewHash == chunkHash)
                .ExecuteUpdateAsync(fm => fm.SetProperty(x => x.SmallFilePreviewHash, (byte[]?)null), ct);

            await _dbContext.FileManifests
                .Where(fm => fm.LargeFilePreviewHash == chunkHash)
                .ExecuteUpdateAsync(fm => fm.SetProperty(x => x.LargeFilePreviewHash, (byte[]?)null), ct);

            await _dbContext.FileManifestChunks
                .Where(fmc => fmc.ChunkHash == chunkHash)
                .ExecuteDeleteAsync(ct);

            await _dbContext.ChunkOwnerships
                .Where(o => o.ChunkHash == chunkHash)
                .ExecuteDeleteAsync(ct);

            await _dbContext.Chunks
                .Where(c => c.Hash == chunkHash)
                .ExecuteDeleteAsync(ct);
        }

        private async Task RegisterOrphanedStorageKeysAsync(HashSet<string> remainingStorageKeys, CancellationToken ct)
        {
            if (remainingStorageKeys.Count == 0)
            {
                return;
            }

            _logger.LogWarning("Found {Count} keys in storage that are not tracked in DB. Registering for garbage collection.",
                remainingStorageKeys.Count);

            DateTime deleteAfter = DateTime.UtcNow.AddDays(1);
            int registered = 0;

            foreach (string uid in remainingStorageKeys)
            {
                ct.ThrowIfCancellationRequested();

                byte[] hash;
                try
                {
                    hash = Hasher.FromHexStringHash(uid);
                }
                catch (ArgumentException)
                {
                    _logger.LogWarning("Skipping invalid storage key: {Uid}", uid);
                    continue;
                }

                bool exists = await _dbContext.Chunks.AnyAsync(c => c.Hash == hash, ct);
                if (exists)
                {
                    continue;
                }

                _dbContext.Chunks.Add(new Chunk
                {
                    Hash = hash,
                    SizeBytes = 0,
                    CompressionAlgorithm = CompressionProcessor.Algorithm,
                    GCScheduledAfter = deleteAfter
                });

                registered++;

                if (registered % BatchSize == 0)
                {
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Registered {Count} orphaned storage keys so far...", registered);
                }
            }

            if (registered > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Registered {Count} orphaned storage keys for garbage collection.", registered);
            }
        }
    }
}
