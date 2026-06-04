// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;
using Cotton.Sync.App.Progress;
using Cotton.Sync.App.Runners;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.App.Tests.TestSupport;
using AppSyncActivity = Cotton.Sync.App.Activities.SyncActivity;
using AppSyncTransferProgress = Cotton.Sync.App.Progress.AppTransferProgress;
using CoreSyncActivity = Cotton.Sync.SyncActivity;
using CoreSyncActivityKind = Cotton.Sync.SyncActivityKind;
using CoreSyncEngine = Cotton.Sync.ISyncEngine;
using CoreSyncPair = Cotton.Sync.SyncPair;
using CoreSyncRunOptions = Cotton.Sync.SyncRunOptions;
using CoreSyncRunResult = Cotton.Sync.SyncRunResult;
using CoreSyncTransferDirection = Cotton.Sync.SyncTransferDirection;
using CoreSyncTransferProgress = Cotton.Sync.SyncTransferProgress;

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

    [Test]
    public async Task RunOnceAsync_PublishesCoreSyncActivities()
    {
        Guid syncPairId = Guid.NewGuid();
        var engine = new FakeSyncEngine
        {
            ActivityToReport = new CoreSyncActivity
            {
                Kind = CoreSyncActivityKind.Conflict,
                RelativePath = "Documents/report.txt",
                Details = "Remote version saved as report conflict.txt",
            },
        };
        var publisher = new InMemoryAppActivityPublisher();
        var observer = new RecordingObserver<AppSyncActivity>();
        using IDisposable subscription = publisher.Subscribe(observer);
        var work = new SyncEnginePairWork(engine, publisher);
        SyncPairSettings syncPair = CreateSyncPair(syncPairId);

        await work.RunOnceAsync(syncPair);

        AppSyncActivity activity = observer.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(engine.LastOptions?.ActivityProgress, Is.Not.Null);
            Assert.That(activity.SyncPairId, Is.EqualTo(syncPairId));
            Assert.That(activity.Type, Is.EqualTo(SyncActivityType.Conflict));
            Assert.That(activity.ItemPath, Is.EqualTo("Documents/report.txt"));
            Assert.That(activity.Message, Does.Contain("Created conflict copy Documents/report.txt"));
            Assert.That(activity.Message, Does.Contain("Remote version saved as report conflict.txt"));
        });
    }

    [Test]
    public async Task RunOnceAsync_PublishesCoreTransferProgress()
    {
        Guid syncPairId = Guid.NewGuid();
        var engine = new FakeSyncEngine
        {
            TransferProgressToReport = new CoreSyncTransferProgress(
                CoreSyncTransferDirection.Upload,
                "Documents/report.txt",
                transferredBytes: 512,
                totalBytes: 1024),
        };
        var publisher = new InMemoryAppTransferProgressPublisher();
        var observer = new RecordingObserver<AppSyncTransferProgress>();
        using IDisposable subscription = publisher.Subscribe(observer);
        var work = new SyncEnginePairWork(engine, progressPublisher: publisher);
        SyncPairSettings syncPair = CreateSyncPair(syncPairId);

        await work.RunOnceAsync(syncPair);

        AppSyncTransferProgress progress = observer.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(engine.LastOptions?.TransferProgress, Is.Not.Null);
            Assert.That(progress.SyncPairId, Is.EqualTo(syncPairId));
            Assert.That(progress.Direction, Is.EqualTo(AppTransferDirection.Upload));
            Assert.That(progress.RelativePath, Is.EqualTo("Documents/report.txt"));
            Assert.That(progress.TransferredBytes, Is.EqualTo(512));
            Assert.That(progress.TotalBytes, Is.EqualTo(1024));
            Assert.That(progress.IsCompleted, Is.False);
        });
    }

    private static SyncPairSettings CreateSyncPair(Guid id)
    {
        return new SyncPairSettings
        {
            Id = id,
            DisplayName = "Documents",
            LocalRootPath = "/home/user/Cotton",
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = "/Documents",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
        };
    }

    private sealed class FakeSyncEngine : CoreSyncEngine
    {
        public CoreSyncActivity? ActivityToReport { get; set; }

        public CoreSyncTransferProgress? TransferProgressToReport { get; set; }

        public CoreSyncRunOptions? LastOptions { get; private set; }

        public CoreSyncPair? LastPair { get; private set; }

        public int RunOnceCallCount { get; private set; }

        public Task<CoreSyncRunResult> RunOnceAsync(
            CoreSyncPair syncPair,
            CoreSyncRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            RunOnceCallCount++;
            LastPair = syncPair;
            LastOptions = options;
            if (ActivityToReport is not null)
            {
                options?.ActivityProgress?.Report(ActivityToReport);
            }

            if (TransferProgressToReport is not null)
            {
                options?.TransferProgress?.Report(TransferProgressToReport);
            }

            return Task.FromResult(new CoreSyncRunResult());
        }
    }

}
