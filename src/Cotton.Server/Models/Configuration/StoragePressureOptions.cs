// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Configuration
{
    /// <summary>
    /// Configures storage pressure.
    /// </summary>
    public sealed class StoragePressureOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether storage pressure monitoring is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// Gets or sets the min free percent.
        /// </summary>
        public int MinFreePercent { get; set; } = 5;
        /// <summary>
        /// Gets or sets the min free bytes.
        /// </summary>
        public long MinFreeBytes { get; set; } = 512L * 1024 * 1024;
        /// <summary>
        /// Gets or sets the check interval seconds.
        /// </summary>
        public int CheckIntervalSeconds { get; set; } = 10;
        /// <summary>
        /// Gets or sets the admin notification cooldown minutes.
        /// </summary>
        public int AdminNotificationCooldownMinutes { get; set; } = 60;

        /// <summary>
        /// Gets the interval between storage pressure checks.
        /// </summary>
        public TimeSpan CheckInterval => TimeSpan.FromSeconds(Math.Clamp(CheckIntervalSeconds, 1, 300));
        /// <summary>
        /// Gets the minimum delay between admin notifications.
        /// </summary>
        public TimeSpan AdminNotificationCooldown => TimeSpan.FromMinutes(Math.Clamp(AdminNotificationCooldownMinutes, 1, 24 * 60));

        /// <summary>
        /// Gets required free bytes.
        /// </summary>
        public long GetRequiredFreeBytes(long totalBytes)
        {
            long safeMinFreeBytes = Math.Max(0, MinFreeBytes);
            int safePercent = Math.Clamp(MinFreePercent, 0, 100);
            long percentReserve = (long)Math.Ceiling(totalBytes * (safePercent / 100d));
            return Math.Max(safeMinFreeBytes, percentReserve);
        }
    }
}
