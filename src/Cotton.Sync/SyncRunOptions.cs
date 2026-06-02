// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Defines options for one synchronization pass.
/// </summary>
public sealed class SyncRunOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether remote file deletes bypass trash.
    /// </summary>
    public bool DeleteRemotePermanently { get; set; }

    /// <summary>
    /// Gets or sets the optional live activity reporter used by UI and CLI clients.
    /// </summary>
    public IProgress<SyncActivity>? ActivityProgress { get; set; }
}
