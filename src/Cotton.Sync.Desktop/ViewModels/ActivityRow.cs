// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels;

/// <summary>
/// Represents one activity row shown in the desktop synchronization log.
/// </summary>
public sealed class ActivityRow
{
    /// <summary>
    /// Gets or sets the UTC timestamp label.
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the activity kind label.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the affected path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional details.
    /// </summary>
    public string Details { get; set; } = string.Empty;
}
