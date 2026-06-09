// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{
    /// <summary>
    /// Runs synchronization passes for configured folder pairs.
    /// </summary>
    public interface ISyncEngine
    {
        /// <summary>
        /// Runs one reconciliation pass for the specified synchronization pair.
        /// </summary>
        Task<SyncRunResult> RunOnceAsync(
            SyncPair syncPair,
            SyncRunOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
