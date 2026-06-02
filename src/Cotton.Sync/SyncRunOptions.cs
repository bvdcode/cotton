// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Defines options for one synchronization pass.
/// </summary>
public sealed class SyncRunOptions
{
    /// <summary>
    /// Default maximum number of data deletes allowed in one non-dry-run pass.
    /// </summary>
    public const int DefaultMaxDeletesPerRun = 100;

    /// <summary>
    /// Default maximum fraction of baseline files that may be deleted in one non-dry-run pass.
    /// </summary>
    public const double DefaultMaxDeleteRatio = 0.5;

    /// <summary>
    /// Minimum baseline size before the delete ratio guard is applied.
    /// </summary>
    public const int DefaultDeleteRatioBaselineThreshold = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the pass should report planned work without mutating local, remote, or state storage.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether remote file deletes bypass trash.
    /// </summary>
    public bool DeleteRemotePermanently { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of local or remote file deletes allowed in one pass.
    /// </summary>
    public int MaxDeletesPerRun { get; set; } = DefaultMaxDeletesPerRun;

    /// <summary>
    /// Gets or sets the maximum fraction of baseline files that may be deleted in one pass.
    /// </summary>
    public double MaxDeleteRatio { get; set; } = DefaultMaxDeleteRatio;

    /// <summary>
    /// Gets or sets the minimum baseline size before <see cref="MaxDeleteRatio" /> is enforced.
    /// </summary>
    public int DeleteRatioBaselineThreshold { get; set; } = DefaultDeleteRatioBaselineThreshold;

    /// <summary>
    /// Gets or sets the optional live activity reporter used by UI and CLI clients.
    /// </summary>
    public IProgress<SyncActivity>? ActivityProgress { get; set; }

    /// <summary>
    /// Gets or sets the optional live progress reporter used by UI and CLI clients.
    /// </summary>
    public IProgress<SyncProgress>? Progress { get; set; }
}
