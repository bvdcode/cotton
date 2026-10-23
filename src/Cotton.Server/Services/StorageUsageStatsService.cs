// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public class StorageUsageStatsService(
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        ChunkUsageService _chunkUsage)
    {
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
            IQueryable<LogicalChunkReference> logicalChunkReferences = BuildLogicalChunkReferences(chunks);

            return await logicalChunkReferences
                .GroupBy(_ => 1)
                .Select(g => new ReferencedChunkStats
                {
                    UniqueChunkCount = g.LongCount(),
                    UniquePlainSizeBytes = g.Sum(x => x.PlainSizeBytes),
                    UniqueStoredSizeBytes = g.Sum(x => x.StoredSizeBytes),
                    LogicalChunkCount = g.Sum(x => x.RefCount),
                    LogicalPlainSizeBytes = g.Sum(x => x.PlainSizeBytes * x.RefCount),
                    DeduplicatedUniqueChunkCount = g.Where(x => x.RefCount > 1).LongCount(),
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? new ReferencedChunkStats();
        }

        private IQueryable<LogicalChunkReference> BuildLogicalChunkReferences(IQueryable<Chunk> chunks)
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
                from chunk in chunks
                join manifestChunk in _dbContext.FileManifestChunks.AsNoTracking()
                    on chunk.Hash equals manifestChunk.ChunkHash
                join manifestReference in manifestReferenceCounts
                    on manifestChunk.FileManifestId equals manifestReference.FileManifestId
                select new
                {
                    chunk.Hash,
                    chunk.PlainSizeBytes,
                    chunk.StoredSizeBytes,
                    manifestReference.RefCount,
                };

            return logicalChunkReferenceRows
                .GroupBy(x => new { x.Hash, x.PlainSizeBytes, x.StoredSizeBytes })
                .Select(g => new LogicalChunkReference
                {
                    PlainSizeBytes = g.Key.PlainSizeBytes,
                    StoredSizeBytes = g.Key.StoredSizeBytes,
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
            public long PlainSizeBytes { get; init; }
            public long StoredSizeBytes { get; init; }
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
