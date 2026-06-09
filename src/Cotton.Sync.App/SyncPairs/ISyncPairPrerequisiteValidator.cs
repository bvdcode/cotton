// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{

    /// <summary>
    /// Performs asynchronous sync-pair checks that require I/O.
    /// </summary>
    public interface ISyncPairPrerequisiteValidator
    {
        /// <summary>
        /// Validates I/O-dependent prerequisites for a sync pair.
        /// </summary>
        Task<IReadOnlyList<SyncPairValidationError>> ValidateAsync(
            SyncPairSettings syncPair,
            CancellationToken cancellationToken = default);
    }
}
