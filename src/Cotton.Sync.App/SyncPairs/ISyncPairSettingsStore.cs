// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{

    /// <summary>
    /// Persists configured sync-pair settings.
    /// </summary>
    public interface ISyncPairSettingsStore
    {
        /// <summary>
        /// Initializes the backing store.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads all configured sync pairs.
        /// </summary>
        Task<IReadOnlyList<SyncPairSettings>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a configured sync pair by identifier.
        /// </summary>
        Task<SyncPairSettings?> GetAsync(Guid syncPairId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or updates a configured sync pair.
        /// </summary>
        Task UpsertAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a configured sync pair.
        /// </summary>
        Task DeleteAsync(Guid syncPairId, CancellationToken cancellationToken = default);
    }
}
