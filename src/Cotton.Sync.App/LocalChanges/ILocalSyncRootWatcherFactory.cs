// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.LocalChanges
{
    /// <summary>
    /// Creates local filesystem watchers for sync roots.
    /// </summary>
    public interface ILocalSyncRootWatcherFactory
    {
        /// <summary>
        /// Creates a watcher for the provided sync pair.
        /// </summary>
        ILocalSyncRootWatcher Create(SyncPairSettings syncPair);
    }
}
