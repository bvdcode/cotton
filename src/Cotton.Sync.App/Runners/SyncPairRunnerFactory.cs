// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Runners;

/// <summary>
/// Creates sync pair runners using a shared sync-work adapter.
/// </summary>
public sealed class SyncPairRunnerFactory : ISyncPairRunnerFactory
{
    private readonly SyncPairRunnerRetryOptions _retryOptions;
    private readonly ISyncPairWork _work;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPairRunnerFactory" /> class.
    /// </summary>
    public SyncPairRunnerFactory(ISyncPairWork work, SyncPairRunnerRetryOptions? retryOptions = null)
    {
        _work = work ?? throw new ArgumentNullException(nameof(work));
        _retryOptions = (retryOptions ?? SyncPairRunnerRetryOptions.Default).Normalize();
    }

    /// <inheritdoc />
    public ISyncPairRunner Create(SyncPairSettings syncPair)
    {
        return new SyncPairRunner(syncPair, _work, _retryOptions);
    }
}
