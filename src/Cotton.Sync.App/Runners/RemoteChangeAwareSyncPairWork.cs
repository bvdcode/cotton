// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Remote;

namespace Cotton.Sync.App.Runners
{

    /// <summary>
    /// Wraps sync pair work with durable remote change-feed checkpoint handling.
    /// </summary>
    public sealed class RemoteChangeAwareSyncPairWork : ISyncPairWork
    {
        private readonly ISyncPairWork _inner;
        private readonly IRemoteChangeFeedReader _remoteChanges;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteChangeAwareSyncPairWork" /> class.
        /// </summary>
        public RemoteChangeAwareSyncPairWork(ISyncPairWork inner, IRemoteChangeFeedReader remoteChanges)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _remoteChanges = remoteChanges ?? throw new ArgumentNullException(nameof(remoteChanges));
        }

        /// <inheritdoc />
        public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(syncPair);
            RemoteChangeFeedBatch remoteBatch = await ReadRemoteChangesAsync(syncPair, cancellationToken)
                .ConfigureAwait(false);

            await _inner.RunOnceAsync(syncPair, cancellationToken).ConfigureAwait(false);

            if (remoteBatch.CursorExpired)
            {
                await _remoteChanges.AcknowledgeFullResyncAsync(remoteBatch, cancellationToken).ConfigureAwait(false);
                return;
            }

            await _remoteChanges.AcknowledgeAsync(remoteBatch, cancellationToken).ConfigureAwait(false);
        }

        private async Task<RemoteChangeFeedBatch> ReadRemoteChangesAsync(
            SyncPairSettings syncPair,
            CancellationToken cancellationToken)
        {
            string syncPairId = syncPair.Id.ToString("D");
            RemoteChangeFeedBatch batch = await _remoteChanges
                .ReadAsync(syncPairId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            while (ShouldReadNextPage(batch))
            {
                batch = await _remoteChanges
                    .ReadFromCursorAsync(syncPairId, batch.NextCursor, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            return batch;
        }

        private static bool ShouldReadNextPage(RemoteChangeFeedBatch batch)
        {
            if (!batch.HasMore || batch.CursorExpired)
            {
                return false;
            }

            if (batch.NextCursor <= batch.SinceCursor)
            {
                throw new InvalidOperationException("Remote change feed reported more pages without advancing the cursor.");
            }

            return true;
        }
    }
}
