// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Defines options for one synchronization pass.
/// </summary>
public sealed class SyncRunOptions
{
    private const int DefaultMaximumDeletesPerRun = 100;

    /// <summary>
    /// Gets or sets a value indicating whether remote file deletes bypass trash.
    /// </summary>
    public bool DeleteRemotePermanently { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of local files that can be removed in one sync pass.
    /// </summary>
    public int MaximumLocalDeletesPerRun { get; set; } = DefaultMaximumDeletesPerRun;

    /// <summary>
    /// Gets or sets the maximum number of remote files that can be removed in one sync pass.
    /// </summary>
    public int MaximumRemoteDeletesPerRun { get; set; } = DefaultMaximumDeletesPerRun;

    /// <summary>
    /// Gets or sets the optional live activity reporter used by UI and CLI clients.
    /// </summary>
    public IProgress<SyncActivity>? ActivityProgress { get; set; }

    /// <summary>
    /// Gets or sets the optional live transfer-progress reporter used by UI clients.
    /// </summary>
    public IProgress<SyncTransferProgress>? TransferProgress { get; set; }

    /// <summary>
    /// Gets or sets the optional aggregate sync-pass progress reporter used by UI clients.
    /// </summary>
    public IProgress<SyncRunProgress>? RunProgress { get; set; }
}
