// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Runners
{
    /// <summary>
    /// Creates runtime runners for configured sync pairs.
    /// </summary>
    public interface ISyncPairRunnerFactory
    {
        /// <summary>
        /// Creates a runner for a sync pair.
        /// </summary>
        ISyncPairRunner Create(SyncPairSettings syncPair);
    }
}
