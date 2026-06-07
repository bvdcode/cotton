// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.LocalChanges;

public sealed class LocalChangeSyncCoordinatorTests
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(25);

    [Test]
    public async Task LocalChanges_AreCoalescedIntoOneSyncRequest()
    {
        SyncPairSettings syncPair = CreatePair(isEnabled: true);
        var watcherFactory = new FakeWatcherFactory();
        var supervisor = new FakeSyncSupervisor();
        var coordinator = new LocalChangeSyncCoordinator(
            new FakeSyncPairSettingsStore([syncPair]),
            supervisor,
            watcherFactory,
            DebounceInterval);
        await coordinator.StartAsync();

        watcherFactory.CreatedWatchers[syncPair.Id].Raise("/home/user/Cotton/a.txt");
        watcherFactory.CreatedWatchers[syncPair.Id].Raise("/home/user/Cotton/b.txt");

        bool observed = await supervisor.WaitForSyncAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(DebounceInterval * 3);
        await coordinator.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.True);
            Assert.That(supervisor.SyncNowCallCount, Is.EqualTo(1));
            Assert.That(supervisor.LastSyncNowPairId, Is.EqualTo(syncPair.Id));
        });
    }

    [Test]
    public async Task LocalChangeStorm_KeepsOnePendingSyncRequestPerPair()
    {
        const int ChangeCount = 1_000;
        SyncPairSettings syncPair = CreatePair(isEnabled: true);
        var watcherFactory = new FakeWatcherFactory();
        var supervisor = new FakeSyncSupervisor();
        var coordinator = new LocalChangeSyncCoordinator(
            new FakeSyncPairSettingsStore([syncPair]),
            supervisor,
            watcherFactory,
            TimeSpan.FromSeconds(5));
        await coordinator.StartAsync();

        for (int index = 0; index < ChangeCount; index++)
        {
            watcherFactory.CreatedWatchers[syncPair.Id].Raise($"/home/user/Cotton/file-{index}.txt");
        }

        int pendingRequestCount = coordinator.PendingRequestCount;
        await coordinator.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pendingRequestCount, Is.EqualTo(1));
            Assert.That(supervisor.SyncNowCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task StartAsync_DoesNotWatchDisabledPairs()
    {
        SyncPairSettings syncPair = CreatePair(isEnabled: false);
        var watcherFactory = new FakeWatcherFactory();
        var coordinator = new LocalChangeSyncCoordinator(
            new FakeSyncPairSettingsStore([syncPair]),
            new FakeSyncSupervisor(),
            watcherFactory,
            DebounceInterval);

        await coordinator.StartAsync();
        await coordinator.StopAsync();

        Assert.That(watcherFactory.CreatedWatchers, Is.Empty);
    }

    [Test]
    public async Task StopAsync_CancelsPendingSyncRequest()
    {
        SyncPairSettings syncPair = CreatePair(isEnabled: true);
        var watcherFactory = new FakeWatcherFactory();
        var supervisor = new FakeSyncSupervisor();
        var coordinator = new LocalChangeSyncCoordinator(
            new FakeSyncPairSettingsStore([syncPair]),
            supervisor,
            watcherFactory,
            TimeSpan.FromMilliseconds(100));
        await coordinator.StartAsync();

        watcherFactory.CreatedWatchers[syncPair.Id].Raise("/home/user/Cotton/a.txt");
        await coordinator.StopAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(150));

        Assert.That(supervisor.SyncNowCallCount, Is.Zero);
    }

    [Test]
    public async Task StopAsync_WaitsForRunningSyncRequest()
    {
        SyncPairSettings syncPair = CreatePair(isEnabled: true);
        var watcherFactory = new FakeWatcherFactory();
        var supervisor = new FakeSyncSupervisor
        {
            BlockSyncNow = true,
        };
        var coordinator = new LocalChangeSyncCoordinator(
            new FakeSyncPairSettingsStore([syncPair]),
            supervisor,
            watcherFactory,
            TimeSpan.Zero);
        await coordinator.StartAsync();

        watcherFactory.CreatedWatchers[syncPair.Id].Raise("/home/user/Cotton/a.txt");
        bool observed = await supervisor.WaitForSyncAsync(TimeSpan.FromSeconds(2));
        Task stopTask = coordinator.StopAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(75));

        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.True);
            Assert.That(stopTask.IsCompleted, Is.False);
        });

        supervisor.ReleaseSyncNow();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(supervisor.SyncNowCallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task StartAsync_CleansCreatedWatchersWhenLaterWatcherFails()
    {
        SyncPairSettings firstPair = CreatePair(isEnabled: true);
        SyncPairSettings secondPair = CreatePair(isEnabled: true);
        var watcherFactory = new FakeWatcherFactory
        {
            FailingStartPairId = secondPair.Id,
        };
        var supervisor = new FakeSyncSupervisor();
        var coordinator = new LocalChangeSyncCoordinator(
            new FakeSyncPairSettingsStore([firstPair, secondPair]),
            supervisor,
            watcherFactory,
            DebounceInterval);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await coordinator.StartAsync());

        watcherFactory.CreatedWatchers[firstPair.Id].Raise("/home/user/Cotton/a.txt");
        await Task.Delay(DebounceInterval * 3);

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Is.EqualTo("Watcher failed to start."));
            Assert.That(watcherFactory.CreatedWatchers[firstPair.Id].StopCallCount, Is.EqualTo(1));
            Assert.That(watcherFactory.CreatedWatchers[firstPair.Id].DisposeAsyncCallCount, Is.EqualTo(1));
            Assert.That(watcherFactory.CreatedWatchers[secondPair.Id].StopCallCount, Is.EqualTo(1));
            Assert.That(watcherFactory.CreatedWatchers[secondPair.Id].DisposeAsyncCallCount, Is.EqualTo(1));
            Assert.That(supervisor.SyncNowCallCount, Is.Zero);
        });
    }

    private static SyncPairSettings CreatePair(bool isEnabled)
    {
        return new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Documents",
            LocalRootPath = "/home/user/Cotton",
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = "/Documents",
            IsEnabled = isEnabled,
            Mode = SyncPairMode.FullMirror,
        };
    }

    private sealed class FakeWatcherFactory : ILocalSyncRootWatcherFactory
    {
        public Dictionary<Guid, FakeWatcher> CreatedWatchers { get; } = [];

        public Guid? FailingStartPairId { get; set; }

        public ILocalSyncRootWatcher Create(SyncPairSettings syncPair)
        {
            var watcher = new FakeWatcher(syncPair.Id);
            if (syncPair.Id == FailingStartPairId)
            {
                watcher.StartException = new InvalidOperationException("Watcher failed to start.");
            }

            CreatedWatchers.Add(syncPair.Id, watcher);
            return watcher;
        }
    }

    private sealed class FakeWatcher : ILocalSyncRootWatcher
    {
        private readonly Guid _syncPairId;

        public FakeWatcher(Guid syncPairId)
        {
            _syncPairId = syncPairId;
        }

        public event EventHandler<LocalSyncRootChange>? Changed;

        public Exception? StartException { get; set; }

        public int DisposeAsyncCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCallCount++;
            return ValueTask.CompletedTask;
        }

        public void Raise(string fullPath)
        {
            Changed?.Invoke(this, new LocalSyncRootChange(
                _syncPairId,
                fullPath,
                LocalSyncRootChangeKind.Changed));
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StartException is not null)
            {
                throw StartException;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSyncPairSettingsStore : ISyncPairSettingsStore
    {
        private readonly IReadOnlyList<SyncPairSettings> _syncPairs;

        public FakeSyncPairSettingsStore(IReadOnlyList<SyncPairSettings> syncPairs)
        {
            _syncPairs = syncPairs;
        }

        public Task DeleteAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SyncPairSettings?> GetAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_syncPairs.SingleOrDefault(pair => pair.Id == syncPairId));
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncPairSettings>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_syncPairs);
        }

        public Task UpsertAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSyncSupervisor : ISyncSupervisor
    {
        private readonly TaskCompletionSource _syncRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSyncNow = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<SyncPairStatus> CurrentStatuses => [];

        public bool BlockSyncNow { get; set; }

        public int SyncNowCallCount { get; private set; }

        public Guid? LastSyncNowPairId { get; private set; }

        public Task PauseAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PauseAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResumeAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SyncAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyncNowCallCount++;
            LastSyncNowPairId = syncPairId;
            _syncRequested.TrySetResult();
            return BlockSyncNow
                ? _releaseSyncNow.Task
                : Task.CompletedTask;
        }

        public async Task<bool> WaitForSyncAsync(TimeSpan timeout)
        {
            Task completed = await Task.WhenAny(_syncRequested.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return completed == _syncRequested.Task;
        }

        public void ReleaseSyncNow()
        {
            _releaseSyncNow.TrySetResult();
        }
    }
}
