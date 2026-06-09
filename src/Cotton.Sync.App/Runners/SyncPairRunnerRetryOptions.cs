// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Runners
{
    /// <summary>
    /// Configures retry behavior for transient sync-pair failures.
    /// </summary>
    public sealed class SyncPairRunnerRetryOptions
    {
        /// <summary>
        /// Gets or sets the default retry policy.
        /// </summary>
        public static SyncPairRunnerRetryOptions Default { get; } = new();

        /// <summary>
        /// Gets or sets the maximum number of attempts for one sync request.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the first retry delay.
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum retry delay.
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Creates a validated copy of the options.
        /// </summary>
        public SyncPairRunnerRetryOptions Normalize()
        {
            if (MaxAttempts <= 0)
            {
                throw new InvalidOperationException("Max attempts must be positive.");
            }

            if (InitialDelay < TimeSpan.Zero)
            {
                throw new InvalidOperationException("Initial retry delay cannot be negative.");
            }

            if (MaxDelay < TimeSpan.Zero)
            {
                throw new InvalidOperationException("Maximum retry delay cannot be negative.");
            }

            return new SyncPairRunnerRetryOptions
            {
                MaxAttempts = MaxAttempts,
                InitialDelay = InitialDelay,
                MaxDelay = MaxDelay,
            };
        }
    }
}
