// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Sync;
using Cotton.Sdk.Sync;
using Cotton.Sync.State;

namespace Cotton.Sync.Remote;

/// <summary>
/// Reads durable remote changes through the SDK and stores per-pair checkpoints.
/// </summary>
public sealed class RemoteChangeFeedReader : IRemoteChangeFeedReader
{
    private readonly ICottonSyncClient _syncClient;
    private readonly ISyncStateStore _stateStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteChangeFeedReader" /> class.
    /// </summary>
    public RemoteChangeFeedReader(ICottonSyncClient syncClient, ISyncStateStore stateStore)
    {
        _syncClient = syncClient ?? throw new ArgumentNullException(nameof(syncClient));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    /// <inheritdoc />
    public async Task<RemoteChangeFeedBatch> ReadAsync(
        string syncPairId,
        int limit = RemoteChangeFeedDefaults.PageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be positive.");
        }

        SyncChangeCursor cursor = await _stateStore.GetChangeCursorAsync(syncPairId, cancellationToken).ConfigureAwait(false);
        SyncChangesResponseDto response = await _syncClient
            .GetChangesAsync(cursor.LastCursor, limit, cancellationToken)
            .ConfigureAwait(false);
        return new RemoteChangeFeedBatch(
            syncPairId,
            response.SinceCursor,
            response.NextCursor,
            response.HasMore,
            response.CursorExpired,
            response.EarliestAvailableCursor,
            response.Changes);
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(RemoteChangeFeedBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        long lastCursor = batch.CursorExpired ? batch.SinceCursor : batch.NextCursor;
        await _stateStore.SaveChangeCursorAsync(
            new SyncChangeCursor
            {
                SyncPairId = batch.SyncPairId,
                LastCursor = lastCursor,
                CursorExpired = batch.CursorExpired,
                EarliestAvailableCursor = batch.EarliestAvailableCursor,
                UpdatedAtUtc = DateTime.UtcNow,
            },
            cancellationToken).ConfigureAwait(false);
    }
}
