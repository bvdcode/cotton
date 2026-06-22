// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Storage.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates chunk usage.
    /// </summary>
    public class ChunkUsageService(
        CottonDbContext _dbContext,
        IStoragePipeline _storage,
        IDatabaseBackupManifestService _backupManifestService,
        DatabaseBackupKeyProvider _backupKeyProvider,
        ILogger<ChunkUsageService> _logger)
    {
        private const int ProtectedHashBatchSize = 500;

        /// <summary>
        /// Filters chunks unreferenced by database.
        /// </summary>
        public IQueryable<Chunk> WhereUnreferencedByDatabase(IQueryable<Chunk> query)
        {
            return query.Where(c => !c.FileManifestChunks.Any()
                && !_dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash || fm.LargeFilePreviewHash == c.Hash)
                && !_dbContext.Users.Any(u => u.AvatarHash == c.Hash));
        }

        /// <summary>
        /// Filters chunks referenced by database.
        /// </summary>
        public IQueryable<Chunk> WhereReferencedByDatabase(IQueryable<Chunk> query)
        {
            return query.Where(c => c.FileManifestChunks.Any()
                || _dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash || fm.LargeFilePreviewHash == c.Hash)
                || _dbContext.Users.Any(u => u.AvatarHash == c.Hash));
        }

        /// <summary>
        /// Filters chunks not protected by storage keys.
        /// </summary>
        public IQueryable<Chunk> WhereNotProtectedByStorageKeys(IQueryable<Chunk> query, IReadOnlyCollection<string> protectedStorageKeys)
        {
            List<byte[]> protectedChunkHashes = GetChunkHashesFromStorageKeys(protectedStorageKeys);
            if (protectedChunkHashes.Count == 0)
            {
                return query;
            }

            return query.Where(c => !protectedChunkHashes.Contains(c.Hash));
        }

        /// <summary>
        /// Returns true if the chunk is referenced by any database row.
        /// </summary>
        public async Task<bool> HasDatabaseReferencesAsync(byte[] chunkHash, CancellationToken ct)
        {
            return await _dbContext.FileManifestChunks.AnyAsync(m => m.ChunkHash == chunkHash, ct)
                || await _dbContext.FileManifests.AnyAsync(fm => fm.SmallFilePreviewHash == chunkHash || fm.LargeFilePreviewHash == chunkHash, ct)
                || await _dbContext.Users.AnyAsync(u => u.AvatarHash == chunkHash, ct);
        }

        /// <summary>
        /// Clears gc schedules for referenced chunks.
        /// </summary>
        public async Task<int> ClearGcSchedulesForReferencedChunksAsync(CancellationToken ct)
        {
            return await WhereReferencedByDatabase(_dbContext.Chunks)
                .Where(c => c.GCScheduledAfter != null)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), ct);
        }

        /// <summary>
        /// Clears gc schedules for protected chunks.
        /// </summary>
        public async Task<int> ClearGcSchedulesForProtectedChunksAsync(
            IReadOnlyCollection<string> protectedStorageKeys,
            CancellationToken ct)
        {
            int cleared = 0;
            List<byte[]> protectedChunkHashes = GetChunkHashesFromStorageKeys(protectedStorageKeys);
            foreach (byte[][] batch in protectedChunkHashes.Chunk(ProtectedHashBatchSize))
            {
                cleared += await _dbContext.Chunks
                    .Where(c => c.GCScheduledAfter != null && batch.Contains(c.Hash))
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), ct);
            }

            return cleared;
        }

        /// <summary>
        /// Clears gc schedule.
        /// </summary>
        public async Task<int> ClearGcScheduleAsync(byte[] chunkHash, CancellationToken ct)
        {
            return await _dbContext.Chunks
                .Where(c => c.Hash == chunkHash && c.GCScheduledAfter != null)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), ct);
        }

        /// <summary>
        /// Gets protected storage keys async.
        /// </summary>
        public async Task<HashSet<string>> GetProtectedStorageKeysAsync(CancellationToken ct)
        {
            string pointerStorageKey = _backupKeyProvider.GetScopedPointerStorageKey();
            HashSet<string> protectedStorageKeys = new(StringComparer.OrdinalIgnoreCase)
            {
                pointerStorageKey,
                MasterKeySentinelStore.SentinelStorageKey
            };

            if (!await _storage.ExistsAsync(pointerStorageKey))
            {
                return protectedStorageKeys;
            }

            var latestBackup = await _backupManifestService.TryGetLatestManifestAsync(ct);
            if (latestBackup is null)
            {
                throw new InvalidOperationException(
                    "Database backup pointer exists, but the latest backup manifest could not be resolved. Aborting chunk garbage collection to avoid deleting backup data.");
            }

            protectedStorageKeys.Add(latestBackup.ManifestStorageKey);
            foreach (var chunk in latestBackup.Manifest.Chunks)
            {
                if (!string.IsNullOrWhiteSpace(chunk.StorageKey))
                {
                    protectedStorageKeys.Add(chunk.StorageKey);
                }
            }

            _logger.LogDebug("Resolved {Count} protected storage keys for chunk garbage collection.", protectedStorageKeys.Count);
            return protectedStorageKeys;
        }

        private static List<byte[]> GetChunkHashesFromStorageKeys(IEnumerable<string> storageKeys)
        {
            List<byte[]> hashes = [];
            foreach (string storageKey in storageKeys)
            {
                try
                {
                    hashes.Add(Hasher.FromHexStringHash(storageKey));
                }
                catch (ArgumentException)
                {
                    // Non-hash storage keys cannot map to Chunk.Hash and are still protected at the storage-key layer.
                }
            }

            return hashes;
        }
    }
}
