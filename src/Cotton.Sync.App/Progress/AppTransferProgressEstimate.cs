// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress
{
    /// <summary>
    /// Contains calculated transfer speed and remaining-time estimates.
    /// </summary>
    public sealed class AppTransferProgressEstimate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppTransferProgressEstimate" /> class.
        /// </summary>
        public AppTransferProgressEstimate(double? speedBytesPerSecond, TimeSpan? estimatedTimeRemaining)
        {
            if (speedBytesPerSecond.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(speedBytesPerSecond.Value);
            }

            if (estimatedTimeRemaining.HasValue && estimatedTimeRemaining.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(estimatedTimeRemaining), "Estimated time remaining cannot be negative.");
            }

            SpeedBytesPerSecond = speedBytesPerSecond;
            EstimatedTimeRemaining = estimatedTimeRemaining;
        }

        /// <summary>
        /// Gets the rolling transfer speed in bytes per second when enough samples are available.
        /// </summary>
        public double? SpeedBytesPerSecond { get; }

        /// <summary>
        /// Gets the estimated time remaining when total bytes and rolling speed are known.
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; }
    }
}
