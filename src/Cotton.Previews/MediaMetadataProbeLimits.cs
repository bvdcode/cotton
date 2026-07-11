// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// Defines process-output and persisted-tag bounds for media metadata probing.
    /// </summary>
    public class MediaMetadataProbeLimits
    {
        /// <summary>
        /// Default maximum UTF-8 size of one persisted media tag value.
        /// </summary>
        public const int DefaultMaxTagValueBytes = 4 * 1024;

        /// <summary>
        /// Default maximum aggregate UTF-8 size of persisted media tags.
        /// </summary>
        public const int DefaultMaxTotalTagBytes = 32 * 1024;

        /// <summary>
        /// Gets the default media metadata probe limits.
        /// </summary>
        public static MediaMetadataProbeLimits Default { get; } = new(
            FfprobeOutputLimits.Default,
            DefaultMaxTagValueBytes,
            DefaultMaxTotalTagBytes);

        /// <summary>
        /// Creates validated process-output and media-tag limits.
        /// </summary>
        public MediaMetadataProbeLimits(
            FfprobeOutputLimits output,
            int maxTagValueBytes,
            int maxTotalTagBytes)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTagValueBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalTagBytes);

            if (maxTagValueBytes > maxTotalTagBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxTagValueBytes),
                    maxTagValueBytes,
                    "An individual tag limit cannot exceed the aggregate tag limit.");
            }

            Output = output;
            MaxTagValueBytes = maxTagValueBytes;
            MaxTotalTagBytes = maxTotalTagBytes;
        }

        /// <summary>
        /// Gets the process-output limits.
        /// </summary>
        public FfprobeOutputLimits Output { get; }

        /// <summary>
        /// Gets the maximum UTF-8 size of one persisted media tag value.
        /// </summary>
        public int MaxTagValueBytes { get; }

        /// <summary>
        /// Gets the maximum aggregate UTF-8 size of persisted media tags.
        /// </summary>
        public int MaxTotalTagBytes { get; }
    }
}
