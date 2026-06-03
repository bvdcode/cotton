// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.SyncApplication;

/// <summary>
/// Coordinates high-level sync-client application commands.
/// </summary>
public interface ISyncApplicationService
{
    /// <summary>
    /// Loads configured sync pairs.
    /// </summary>
    Task<IReadOnlyList<SyncPairSettings>> ListSyncPairsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a configured sync pair by identifier.
    /// </summary>
    Task<SyncPairSettings?> GetSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and persists a sync pair.
    /// </summary>
    Task<SyncPairSaveResult> SaveSyncPairAsync(
        SyncPairSettings syncPair,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configured sync pair.
    /// </summary>
    Task DeleteSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default);
}
