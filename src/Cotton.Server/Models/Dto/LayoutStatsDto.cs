// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>
namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the layout stats API payload.
    /// </summary>
    public class LayoutStatsDto
    {
        /// <summary>
        /// Gets or sets size bytes.
        /// </summary>
        public long SizeBytes { get; init; }
        /// <summary>
        /// Gets or sets layout id.
        /// </summary>
        public Guid LayoutId { get; init; }
        /// <summary>
        /// Gets or sets node count.
        /// </summary>
        public int NodeCount { get; init; }
        /// <summary>
        /// Gets or sets file count.
        /// </summary>
        public int FileCount { get; init; }
    }
}
