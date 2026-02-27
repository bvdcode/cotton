using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
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

                    // double-check storage in case of race condition
                    var stillExists = await _storage.ExistsAsync(uid);
                    if (stillExists)
                    {
                        _logger.LogWarning("Chunk {Uid} not found in initial storage key scan, but exists on direct check. Skipping as missing.", uid);
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
            // 1) If the missing chunk is used by previews only, silently clear preview references.
            bool referencedByFileData = await _dbContext.FileManifestChunks
                .AnyAsync(fmc => fmc.ChunkHash == chunkHash, ct);

            bool referencedByPreview = await _dbContext.FileManifests
                .AnyAsync(fm => fm.SmallFilePreviewHash == chunkHash || fm.LargeFilePreviewHash == chunkHash, ct);

            if (referencedByPreview)
            {
                await _dbContext.FileManifests
                    .Where(fm => fm.SmallFilePreviewHash == chunkHash)
                    .ExecuteUpdateAsync(fm => fm.SetProperty(x => x.SmallFilePreviewHash, (byte[]?)null), ct);

                await _dbContext.FileManifests
                    .Where(fm => fm.LargeFilePreviewHash == chunkHash)
                    .ExecuteUpdateAsync(fm => fm.SetProperty(x => x.LargeFilePreviewHash, (byte[]?)null), ct);

                // If this chunk was preview-only, we don't care: no notifications, no DB cleanups.
                if (!referencedByFileData)
                {
                    return;
                }
            }

            // 2) For actual file data chunks: notify affected users, but do not delete anything.
            var affectedManifestIds = await _dbContext.FileManifestChunks
                .Where(fmc => fmc.ChunkHash == chunkHash)
                .Select(fmc => fmc.FileManifestId)
                .Distinct()
                .ToListAsync(ct);

            var affectedNodeFiles = await _dbContext.NodeFiles
                .Where(nf => affectedManifestIds.Contains(nf.FileManifestId))
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

                long sizeBytes = await _storage.GetSizeAsync(uid);
                if (sizeBytes == 0 && uid != Hasher.ZeroHashHexString)
                {
                    throw new InvalidOperationException(
                        $"Storage key {uid} has size 0, which is unexpected for non-empty content. Aborting consistency job to avoid data loss.");
                }
                _dbContext.Chunks.Add(new Chunk
                {
                    Hash = hash,
                    SizeBytes = sizeBytes,
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
