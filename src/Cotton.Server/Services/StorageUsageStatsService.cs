// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Calculates storage usage statistics for administrative diagnostics.
    /// </summary>
    public class StorageUsageStatsService(
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        ChunkUsageService _chunkUsage)
    {
        /// <summary>
        /// Gets a storage usage snapshot.
        /// </summary>
        public async Task<StorageUsageStatsDto> GetAsync(
            DateTime nowUtc,
            IReadOnlyCollection<string> protectedStorageKeys,
            CancellationToken cancellationToken)
        {
            IQueryable<Chunk> chunks = _dbContext.Chunks.AsNoTracking();
            TotalChunkStats totalStats = await LoadTotalChunkStatsAsync(chunks, cancellationToken);
            ReferencedChunkStats referencedStats = await LoadReferencedChunkStatsAsync(chunks, cancellationToken);
            PendingGcStats pendingGcStats = await LoadPendingGcStatsAsync(chunks, protectedStorageKeys, nowUtc, cancellationToken);

            return new StorageUsageStatsDto
            {
                StorageType = _settings.GetServerSettings().StorageType.ToString(),
                TotalUniqueChunkCount = totalStats.UniqueChunkCount,
                TotalUniqueChunkPlainSizeBytes = totalStats.UniquePlainSizeBytes,
                TotalUniqueChunkStoredSizeBytes = totalStats.UniqueStoredSizeBytes,
                ReferencedUniqueChunkCount = referencedStats.UniqueChunkCount,
                ReferencedUniqueChunkPlainSizeBytes = referencedStats.UniquePlainSizeBytes,
                ReferencedUniqueChunkStoredSizeBytes = referencedStats.UniqueStoredSizeBytes,
                ReferencedLogicalChunkCount = referencedStats.LogicalChunkCount,
                ReferencedLogicalPlainSizeBytes = referencedStats.LogicalPlainSizeBytes,
                DeduplicatedUniqueChunkCount = referencedStats.DeduplicatedUniqueChunkCount,
                DedupSavedBytes = Math.Max(0, referencedStats.LogicalPlainSizeBytes - referencedStats.UniquePlainSizeBytes),
                CompressionSavedBytes = Math.Max(0, totalStats.UniquePlainSizeBytes - totalStats.UniqueStoredSizeBytes),
                PendingGcChunkCount = pendingGcStats.ChunkCount,
                PendingGcStoredSizeBytes = pendingGcStats.StoredSizeBytes,
                OverdueGcChunkCount = pendingGcStats.OverdueChunkCount,
                OverdueGcStoredSizeBytes = pendingGcStats.OverdueStoredSizeBytes,
            };
        }

        private static async Task<TotalChunkStats> LoadTotalChunkStatsAsync(
            IQueryable<Chunk> chunks,
            CancellationToken cancellationToken)
        {
            return await chunks
                .GroupBy(_ => 1)
                .Select(g => new TotalChunkStats
                {
                    UniqueChunkCount = g.LongCount(),
                    UniquePlainSizeBytes = g.Sum(x => x.PlainSizeBytes),
                    UniqueStoredSizeBytes = g.Sum(x => x.StoredSizeBytes),
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? new TotalChunkStats();
        }

        private async Task<ReferencedChunkStats> LoadReferencedChunkStatsAsync(
            IQueryable<Chunk> chunks,
            CancellationToken cancellationToken)
        {
            IQueryable<LogicalChunkReference> logicalChunkReferences = BuildLogicalChunkReferences();

            return await (
                from chunk in chunks
                join reference in logicalChunkReferences on chunk.Hash equals reference.ChunkHash
                group new { chunk, reference } by 1 into g
                select new ReferencedChunkStats
                {
                    UniqueChunkCount = g.LongCount(),
                    UniquePlainSizeBytes = g.Sum(x => x.chunk.PlainSizeBytes),
                    UniqueStoredSizeBytes = g.Sum(x => x.chunk.StoredSizeBytes),
                    LogicalChunkCount = g.Sum(x => x.reference.RefCount),
                    LogicalPlainSizeBytes = g.Sum(x => x.chunk.PlainSizeBytes * x.reference.RefCount),
                    DeduplicatedUniqueChunkCount = g.Where(x => x.reference.RefCount > 1).LongCount(),
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? new ReferencedChunkStats();
        }

        private IQueryable<LogicalChunkReference> BuildLogicalChunkReferences()
        {
            var manifestReferenceCounts = _dbContext.NodeFiles
                .AsNoTracking()
                .GroupBy(x => x.FileManifestId)
                .Select(g => new
                {
                    FileManifestId = g.Key,
                    RefCount = g.LongCount(),
                });

            var logicalChunkReferenceRows =
                from manifestChunk in _dbContext.FileManifestChunks.AsNoTracking()
                join manifestReference in manifestReferenceCounts
                    on manifestChunk.FileManifestId equals manifestReference.FileManifestId
                select new
                {
                    manifestChunk.ChunkHash,
                    manifestReference.RefCount,
                };

            return logicalChunkReferenceRows
                .GroupBy(x => x.ChunkHash)
                .Select(g => new LogicalChunkReference
                {
                    ChunkHash = g.Key,
                    RefCount = g.Sum(x => x.RefCount),
                });
        }

        private async Task<PendingGcStats> LoadPendingGcStatsAsync(
            IQueryable<Chunk> chunks,
            IReadOnlyCollection<string> protectedStorageKeys,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            IQueryable<Chunk> pendingGc = _chunkUsage
                .WhereNotProtectedByStorageKeys(
                    _chunkUsage.WhereUnreferencedByDatabase(chunks),
                    protectedStorageKeys)
                .Where(c => c.GCScheduledAfter != null);

            return await pendingGc
                .GroupBy(_ => 1)
                .Select(g => new PendingGcStats
                {
                    ChunkCount = g.LongCount(),
                    StoredSizeBytes = g.Sum(x => x.StoredSizeBytes),
                    OverdueChunkCount = g.Where(x => x.GCScheduledAfter <= nowUtc).LongCount(),
                    OverdueStoredSizeBytes = g.Where(x => x.GCScheduledAfter <= nowUtc)
                        .Sum(x => (long?)x.StoredSizeBytes) ?? 0L,
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? new PendingGcStats();
        }

        private class LogicalChunkReference
        {
            public byte[] ChunkHash { get; init; } = null!;
            public long RefCount { get; init; }
        }

        private class TotalChunkStats
        {
            public long UniqueChunkCount { get; init; }
            public long UniquePlainSizeBytes { get; init; }
            public long UniqueStoredSizeBytes { get; init; }
        }

        private class ReferencedChunkStats
        {
            public long UniqueChunkCount { get; init; }
            public long UniquePlainSizeBytes { get; init; }
            public long UniqueStoredSizeBytes { get; init; }
            public long LogicalChunkCount { get; init; }
            public long LogicalPlainSizeBytes { get; init; }
            public long DeduplicatedUniqueChunkCount { get; init; }
        }

        private class PendingGcStats
        {
            public long ChunkCount { get; init; }
            public long StoredSizeBytes { get; init; }
            public long OverdueChunkCount { get; init; }
            public long OverdueStoredSizeBytes { get; init; }
        }
    }
}
