// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.LocalChanges
{
    /// <summary>
    /// Coordinates local filesystem changes with sync requests.
    /// </summary>
    public interface ILocalChangeSyncCoordinator
    {
        /// <summary>
        /// Starts local change observation for configured sync pairs.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops local change observation.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
