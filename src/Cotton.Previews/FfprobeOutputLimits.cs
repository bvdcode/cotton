// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public class FfprobeOutputLimits
    {
        public const int DefaultMaxStandardOutputBytes = 256 * 1024;

        public const int DefaultMaxStandardErrorBytes = 64 * 1024;

        public static FfprobeOutputLimits Default { get; } = new(
            DefaultMaxStandardOutputBytes,
            DefaultMaxStandardErrorBytes);

        public FfprobeOutputLimits(int maxStandardOutputBytes, int maxStandardErrorBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStandardOutputBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStandardErrorBytes);

            MaxStandardOutputBytes = maxStandardOutputBytes;
            MaxStandardErrorBytes = maxStandardErrorBytes;
        }

        public int MaxStandardOutputBytes { get; }

        public int MaxStandardErrorBytes { get; }
    }
}
