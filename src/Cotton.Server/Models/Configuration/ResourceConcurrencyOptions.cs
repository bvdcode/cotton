// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Configuration
{
    /// <summary>
    /// Configures process-wide concurrency for expensive server resources.
    /// </summary>
    public class ResourceConcurrencyOptions
    {
        /// <summary>
        /// Configuration section name.
        /// </summary>
        public const string SectionName = "ResourceConcurrency";

        /// <summary>
        /// Gets or sets the maximum number of concurrent HLS transcodes.
        /// </summary>
        public int HlsTranscodes { get; set; } = 2;

        /// <summary>
        /// Gets or sets the maximum number of concurrently streamed archives.
        /// </summary>
        public int ArchiveStreams { get; set; } = 4;

        /// <summary>
        /// Validates configured limits.
        /// </summary>
        public void Validate()
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HlsTranscodes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ArchiveStreams);
        }
    }
}
