// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels;

/// <summary>
/// Displays one recent synchronization activity.
/// </summary>
internal sealed class ActivityRowViewModel
{
    public string Time { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;

    public bool HasPath => !string.IsNullOrWhiteSpace(Path);
}
