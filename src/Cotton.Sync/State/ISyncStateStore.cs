// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State;

/// <summary>
/// Persists sync baselines for one or more sync pairs.
/// </summary>
public interface ISyncStateStore
{
    /// <summary>
    /// Initializes durable storage if it has not been created yet.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all entries for a sync pair.
    /// </summary>
    Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(string syncPairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the remote change-feed checkpoint for a sync pair.
    /// </summary>
    Task<SyncChangeCursor> GetChangeCursorAsync(string syncPairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one entry by relative path.
    /// </summary>
    Task<SyncStateEntry?> GetAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates one sync entry.
    /// </summary>
    Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the remote change-feed checkpoint for a sync pair.
    /// </summary>
    Task SaveChangeCursorAsync(SyncChangeCursor cursor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one sync entry by relative path.
    /// </summary>
    Task DeleteAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces every entry for a sync pair with a new snapshot.
    /// </summary>
    Task ReplacePairAsync(string syncPairId, IReadOnlyCollection<SyncStateEntry> entries, CancellationToken cancellationToken = default);
}
