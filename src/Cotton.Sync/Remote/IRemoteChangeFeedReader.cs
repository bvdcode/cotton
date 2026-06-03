// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote;

/// <summary>
/// Reads durable remote change-feed pages and advances checkpoints after successful processing.
/// </summary>
public interface IRemoteChangeFeedReader
{
    /// <summary>
    /// Reads the next remote change-feed page for a sync pair without advancing its checkpoint.
    /// </summary>
    Task<RemoteChangeFeedBatch> ReadAsync(
        string syncPairId,
        int limit = RemoteChangeFeedDefaults.PageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances or marks the sync pair checkpoint after a batch has been processed.
    /// </summary>
    Task AcknowledgeAsync(RemoteChangeFeedBatch batch, CancellationToken cancellationToken = default);
}
