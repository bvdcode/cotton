// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{
    internal record DesktopTransferProgressSnapshot(
        Guid SyncPairId,
        SyncTransferDirection Direction,
        string RelativePath,
        long TransferredBytes,
        long? TotalBytes,
        bool IsCompleted,
        DateTime OccurredAtUtc,
        double? SpeedBytesPerSecond = null,
        TimeSpan? EstimatedTimeRemaining = null);
}
