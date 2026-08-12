// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public class MediaMetadataProbeLimits
    {
        public const int DefaultMaxTagValueBytes = 4 * 1024;

        public const int DefaultMaxTotalTagBytes = 32 * 1024;

        public static MediaMetadataProbeLimits Default { get; } = new(
            FfprobeOutputLimits.Default,
            DefaultMaxTagValueBytes,
            DefaultMaxTotalTagBytes);

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

        public FfprobeOutputLimits Output { get; }

        public int MaxTagValueBytes { get; }

        public int MaxTotalTagBytes { get; }
    }
}
