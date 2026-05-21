// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Configuration;

public sealed class StoragePressureOptions
{
    public bool Enabled { get; set; } = true;
    public int MinFreePercent { get; set; } = 5;
    public long MinFreeBytes { get; set; } = 512L * 1024 * 1024;
    public int CheckIntervalSeconds { get; set; } = 10;
    public int AdminNotificationCooldownMinutes { get; set; } = 60;

    public TimeSpan CheckInterval => TimeSpan.FromSeconds(Math.Clamp(CheckIntervalSeconds, 1, 300));
    public TimeSpan AdminNotificationCooldown => TimeSpan.FromMinutes(Math.Clamp(AdminNotificationCooldownMinutes, 1, 24 * 60));

    public long GetRequiredFreeBytes(long totalBytes)
    {
        long safeMinFreeBytes = Math.Max(0, MinFreeBytes);
        int safePercent = Math.Clamp(MinFreePercent, 0, 100);
        long percentReserve = (long)Math.Ceiling(totalBytes * (safePercent / 100d));
        return Math.Max(safeMinFreeBytes, percentReserve);
    }
}
