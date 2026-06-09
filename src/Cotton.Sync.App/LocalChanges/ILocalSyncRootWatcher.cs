// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.LocalChanges
{

    /// <summary>
    /// Watches one local sync root for filesystem changes.
    /// </summary>
    public interface ILocalSyncRootWatcher : IAsyncDisposable
    {
        /// <summary>
        /// Raised when the local sync root may need reconciliation.
        /// </summary>
        event EventHandler<LocalSyncRootChange>? Changed;

        /// <summary>
        /// Starts watching the local sync root.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops watching the local sync root.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
