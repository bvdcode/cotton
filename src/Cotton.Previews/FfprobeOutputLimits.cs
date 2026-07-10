// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// Defines memory bounds for captured ffprobe process output.
    /// </summary>
    public class FfprobeOutputLimits
    {
        /// <summary>
        /// Default maximum captured standard-output size.
        /// </summary>
        public const int DefaultMaxStandardOutputBytes = 256 * 1024;

        /// <summary>
        /// Default maximum captured standard-error size.
        /// </summary>
        public const int DefaultMaxStandardErrorBytes = 64 * 1024;

        /// <summary>
        /// Gets the default output limits.
        /// </summary>
        public static FfprobeOutputLimits Default { get; } = new(
            DefaultMaxStandardOutputBytes,
            DefaultMaxStandardErrorBytes);

        /// <summary>
        /// Creates validated output limits.
        /// </summary>
        public FfprobeOutputLimits(int maxStandardOutputBytes, int maxStandardErrorBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStandardOutputBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStandardErrorBytes);

            MaxStandardOutputBytes = maxStandardOutputBytes;
            MaxStandardErrorBytes = maxStandardErrorBytes;
        }

        /// <summary>
        /// Gets the maximum captured standard-output size.
        /// </summary>
        public int MaxStandardOutputBytes { get; }

        /// <summary>
        /// Gets the maximum captured standard-error size.
        /// </summary>
        public int MaxStandardErrorBytes { get; }
    }
}
