// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the gc chunk timeline bucket API payload.
    /// </summary>
    public class GcChunkTimelineBucketDto
    {
        /// <summary>
        /// Gets or sets bucket start utc.
        /// </summary>
        public DateTime BucketStartUtc { get; init; }

        /// <summary>
        /// Gets or sets chunk count.
        /// </summary>
        public long ChunkCount { get; init; }

        /// <summary>
        /// Gets or sets size bytes.
        /// </summary>
        public long SizeBytes { get; init; }
    }
}
