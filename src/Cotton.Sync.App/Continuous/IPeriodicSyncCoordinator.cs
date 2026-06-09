// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Continuous
{
    /// <summary>
    /// Coordinates periodic safety reconciliation requests.
    /// </summary>
    public interface IPeriodicSyncCoordinator
    {
        /// <summary>
        /// Starts periodic synchronization.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops periodic synchronization.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
