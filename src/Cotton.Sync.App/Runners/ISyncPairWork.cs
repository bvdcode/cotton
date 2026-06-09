// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Runners
{
    /// <summary>
    /// Runs one deterministic synchronization pass for a sync pair.
    /// </summary>
    public interface ISyncPairWork
    {
        /// <summary>
        /// Runs one synchronization pass.
        /// </summary>
        Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default);
    }
}
