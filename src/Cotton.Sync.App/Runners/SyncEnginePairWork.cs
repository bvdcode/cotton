// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;
using CoreSyncEngine = Cotton.Sync.ISyncEngine;
using CoreSyncPair = Cotton.Sync.SyncPair;

namespace Cotton.Sync.App.Runners;

/// <summary>
/// Runs sync pair work through the headless Cotton sync engine.
/// </summary>
public sealed class SyncEnginePairWork : ISyncPairWork
{
    private readonly CoreSyncEngine _syncEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEnginePairWork" /> class.
    /// </summary>
    public SyncEnginePairWork(CoreSyncEngine syncEngine)
    {
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
    }

    /// <inheritdoc />
    public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncPair);
        _ = await _syncEngine
            .RunOnceAsync(ToCorePair(syncPair), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static CoreSyncPair ToCorePair(SyncPairSettings syncPair)
    {
        return new CoreSyncPair
        {
            SyncPairId = syncPair.Id.ToString("D"),
            LocalRootPath = syncPair.LocalRootPath,
            RemoteRootNodeId = syncPair.RemoteRootNodeId,
        };
    }
}
