// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.RemoteChanges;

/// <summary>
/// Coordinates remote realtime changes with sync requests.
/// </summary>
public interface IRemoteChangeSyncCoordinator
{
    /// <summary>
    /// Starts remote change observation.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops remote change observation.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
