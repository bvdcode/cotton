// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Runners;
using Cotton.Sync.App.SyncPairs;
using CoreSyncEngine = Cotton.Sync.ISyncEngine;
using CoreSyncPair = Cotton.Sync.SyncPair;
using CoreSyncRunOptions = Cotton.Sync.SyncRunOptions;
using CoreSyncRunResult = Cotton.Sync.SyncRunResult;

namespace Cotton.Sync.App.Tests.Runners;

public sealed class SyncEnginePairWorkTests
{
    [Test]
    public async Task RunOnceAsync_MapsAppSyncPairToCoreSyncPair()
    {
        var engine = new FakeSyncEngine();
        var work = new SyncEnginePairWork(engine);
        var syncPair = new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Documents",
            LocalRootPath = "/home/user/Cotton",
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = "/Documents",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
        };

        await work.RunOnceAsync(syncPair);

        Assert.Multiple(() =>
        {
            Assert.That(engine.RunOnceCallCount, Is.EqualTo(1));
            Assert.That(engine.LastPair, Is.Not.Null);
            Assert.That(engine.LastPair!.SyncPairId, Is.EqualTo(syncPair.Id.ToString("D")));
            Assert.That(engine.LastPair.LocalRootPath, Is.EqualTo(syncPair.LocalRootPath));
            Assert.That(engine.LastPair.RemoteRootNodeId, Is.EqualTo(syncPair.RemoteRootNodeId));
        });
    }

    private sealed class FakeSyncEngine : CoreSyncEngine
    {
        public CoreSyncPair? LastPair { get; private set; }

        public int RunOnceCallCount { get; private set; }

        public Task<CoreSyncRunResult> RunOnceAsync(
            CoreSyncPair syncPair,
            CoreSyncRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            RunOnceCallCount++;
            LastPair = syncPair;
            return Task.FromResult(new CoreSyncRunResult());
        }
    }
}
