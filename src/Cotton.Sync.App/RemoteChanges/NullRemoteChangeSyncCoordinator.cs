// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.RemoteChanges
{

    /// <summary>
    /// No-op remote change coordinator used when realtime sync is not configured.
    /// </summary>
    public sealed class NullRemoteChangeSyncCoordinator : IRemoteChangeSyncCoordinator
    {
        /// <summary>
        /// Gets the shared no-op coordinator instance.
        /// </summary>
        public static NullRemoteChangeSyncCoordinator Instance { get; } = new();

        private NullRemoteChangeSyncCoordinator()
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
