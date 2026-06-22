// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the gc chunk timeline API payload.
    /// </summary>
    public class GcChunkTimelineDto
    {
        /// <summary>
        /// Gets or sets bucket.
        /// </summary>
        public string Bucket { get; init; } = "hour";

        /// <summary>
        /// Gets or sets timezone offset minutes.
        /// </summary>
        public int TimezoneOffsetMinutes { get; init; }

        /// <summary>
        /// Gets or sets from.
        /// </summary>
        public DateTime From { get; init; }

        /// <summary>
        /// Gets or sets to.
        /// </summary>
        public DateTime To { get; init; }

        /// <summary>
        /// Gets or sets generated at.
        /// </summary>
        public DateTime GeneratedAt { get; init; }

        /// <summary>
        /// Gets or sets total chunks.
        /// </summary>
        public long TotalChunks { get; init; }

        /// <summary>
        /// Gets or sets total size bytes.
        /// </summary>
        public long TotalSizeBytes { get; init; }

        /// <summary>
        /// Gets or sets buckets.
        /// </summary>
        public IReadOnlyList<GcChunkTimelineBucketDto> Buckets { get; init; } = [];

        /// <summary>
        /// Gets or sets storage.
        /// </summary>
        public StorageUsageStatsDto Storage { get; init; } = new();
    }
}
