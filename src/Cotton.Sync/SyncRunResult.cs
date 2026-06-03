// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Contains the activities emitted by one synchronization pass.
/// </summary>
public sealed class SyncRunResult
{
    /// <summary>
    /// Gets the activities emitted during the pass.
    /// </summary>
    public List<SyncActivity> Activities { get; } = [];
}
