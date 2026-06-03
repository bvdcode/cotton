// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Configuration
{
    /// <summary>Configures durable synchronization change-feed retention.</summary>
    public sealed class SyncChangeRetentionOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "SyncChangeRetention";

        /// <summary>Minimum supported retention period in days.</summary>
        public const int MinimumRetentionDays = 1;

        /// <summary>Number of days sync changes are retained before pruning.</summary>
        public int RetentionDays { get; set; } = 30;
    }
}
