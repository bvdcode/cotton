// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Status;

namespace Cotton.Sync.App.Runners;

/// <summary>
/// Controls runtime lifecycle for one configured sync pair.
/// </summary>
public interface ISyncPairRunner
{
    /// <summary>
    /// Gets the sync pair identifier.
    /// </summary>
    Guid SyncPairId { get; }

    /// <summary>
    /// Gets the current runner status.
    /// </summary>
    SyncPairStatus Status { get; }

    /// <summary>
    /// Starts the runner.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses synchronization for this runner.
    /// </summary>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes synchronization for this runner.
    /// </summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests an immediate synchronization pass.
    /// </summary>
    Task SyncNowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the runner.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
