// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.LocalChanges
{
    /// <summary>
    /// No-op local change coordinator used when continuous local sync is not configured.
    /// </summary>
    public class NullLocalChangeSyncCoordinator : ILocalChangeSyncCoordinator
    {
        /// <summary>
        /// Gets the shared no-op coordinator instance.
        /// </summary>
        public static NullLocalChangeSyncCoordinator Instance { get; } = new();

        private NullLocalChangeSyncCoordinator()
        {
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
