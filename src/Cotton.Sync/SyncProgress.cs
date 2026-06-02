// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Represents live progress for a synchronization pass.
/// </summary>
public sealed class SyncProgress
{
    /// <summary>
    /// Gets or sets the user-facing progress message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the completed item count when determinate progress is available.
    /// </summary>
    public int? Current { get; set; }

    /// <summary>
    /// Gets or sets the total item count when determinate progress is available.
    /// </summary>
    public int? Total { get; set; }
}
