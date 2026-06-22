// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the storage usage stats API payload.
    /// </summary>
    public class StorageUsageStatsDto
    {
        /// <summary>
        /// Gets or sets storage type.
        /// </summary>
        public string StorageType { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets total unique chunk count.
        /// </summary>
        public long TotalUniqueChunkCount { get; init; }

        /// <summary>
        /// Gets or sets total unique chunk plain size bytes.
        /// </summary>
        public long TotalUniqueChunkPlainSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets total unique chunk stored size bytes.
        /// </summary>
        public long TotalUniqueChunkStoredSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets referenced unique chunk count.
        /// </summary>
        public long ReferencedUniqueChunkCount { get; init; }

        /// <summary>
        /// Gets or sets referenced unique chunk plain size bytes.
        /// </summary>
        public long ReferencedUniqueChunkPlainSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets referenced unique chunk stored size bytes.
        /// </summary>
        public long ReferencedUniqueChunkStoredSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets referenced logical chunk count.
        /// </summary>
        public long ReferencedLogicalChunkCount { get; init; }

        /// <summary>
        /// Gets or sets referenced logical plain size bytes.
        /// </summary>
        public long ReferencedLogicalPlainSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets deduplicated unique chunk count.
        /// </summary>
        public long DeduplicatedUniqueChunkCount { get; init; }

        /// <summary>
        /// Gets or sets dedup saved bytes.
        /// </summary>
        public long DedupSavedBytes { get; init; }

        /// <summary>
        /// Gets or sets compression saved bytes.
        /// </summary>
        public long CompressionSavedBytes { get; init; }

        /// <summary>
        /// Gets or sets pending gc chunk count.
        /// </summary>
        public long PendingGcChunkCount { get; init; }

        /// <summary>
        /// Gets or sets pending gc stored size bytes.
        /// </summary>
        public long PendingGcStoredSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets overdue gc chunk count.
        /// </summary>
        public long OverdueGcChunkCount { get; init; }

        /// <summary>
        /// Gets or sets overdue gc stored size bytes.
        /// </summary>
        public long OverdueGcStoredSizeBytes { get; init; }
    }
}
