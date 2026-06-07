// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Status;

namespace Cotton.Sync.App.Supervision;

/// <summary>
/// Coordinates runtime sync pair runners.
/// </summary>
public interface ISyncSupervisor
{
    /// <summary>
    /// Gets current per-pair statuses.
    /// </summary>
    IReadOnlyList<SyncPairStatus> CurrentStatuses { get; }

    /// <summary>
    /// Starts runners for configured sync pairs.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts runners for configured sync pairs and optionally pauses them before publishing status.
    /// </summary>
    Task StartAsync(bool startPaused, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests one sync pass for every runner.
    /// </summary>
    Task SyncAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests one sync pass for a runner.
    /// </summary>
    Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses every runner.
    /// </summary>
    Task PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses one runner.
    /// </summary>
    Task PauseAsync(Guid syncPairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes every runner.
    /// </summary>
    Task ResumeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes one runner.
    /// </summary>
    Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all runners.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
