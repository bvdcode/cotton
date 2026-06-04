// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.Supervision;

public sealed class SyncSupervisorTests
{
    [Test]
    public async Task StartAsync_CreatesStartsRunnersAndPublishesStatus()
    {
        SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
        SyncPairSettings pictures = CreatePair("Pictures", isEnabled: false);
        var store = new FakeSyncPairSettingsStore([documents, pictures]);
        var factory = new FakeSyncPairRunnerFactory();
        var publisher = new InMemoryAppStatusPublisher(new SyncAppStatus(true, [], DateTime.UtcNow));
        var supervisor = new SyncSupervisor(store, factory, publisher);

        await supervisor.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(store.InitializeCallCount, Is.EqualTo(1));
            Assert.That(factory.CreatedRunners, Has.Count.EqualTo(2));
            Assert.That(factory.CreatedRunners[documents.Id].StartCallCount, Is.EqualTo(1));
            Assert.That(factory.CreatedRunners[pictures.Id].StartCallCount, Is.EqualTo(1));
            Assert.That(publisher.Current.IsAuthenticated, Is.True);
            Assert.That(
                publisher.Current.SyncPairs.Select(status => status.State),
                Is.EqualTo(new[] { SyncPairRunState.Idle, SyncPairRunState.Disabled }));
        });
    }

    [Test]
    public async Task StartAsync_StopsExistingRunnersBeforeReplacingThem()
    {
        SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
        var store = new FakeSyncPairSettingsStore([documents]);
        var factory = new FakeSyncPairRunnerFactory();
        var supervisor = new SyncSupervisor(store, factory, new InMemoryAppStatusPublisher());
        await supervisor.StartAsync();
        FakeSyncPairRunner firstRunner = factory.CreatedRunners[documents.Id];

        await supervisor.StartAsync();

        FakeSyncPairRunner secondRunner = factory.CreatedRunners[documents.Id];
        Assert.Multiple(() =>
        {
            Assert.That(firstRunner.StopCallCount, Is.EqualTo(1));
            Assert.That(secondRunner, Is.Not.SameAs(firstRunner));
            Assert.That(secondRunner.StartCallCount, Is.EqualTo(1));
            Assert.That(factory.AllCreatedRunners, Has.Count.EqualTo(2));
        });
    }


    [Test]
    public async Task PauseAndResumeAsync_UpdateSelectedRunnerAndPublishStatus()
    {
        SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
        SyncPairSettings pictures = CreatePair("Pictures", isEnabled: true);
        var factory = new FakeSyncPairRunnerFactory();
        var publisher = new InMemoryAppStatusPublisher();
        var supervisor = new SyncSupervisor(
            new FakeSyncPairSettingsStore([documents, pictures]),
            factory,
            publisher);
        await supervisor.StartAsync();

        await supervisor.PauseAsync(pictures.Id);
        SyncPairRunState pausedState = factory.CreatedRunners[pictures.Id].Status.State;
        await supervisor.ResumeAsync(pictures.Id);

        Assert.Multiple(() =>
        {
            Assert.That(pausedState, Is.EqualTo(SyncPairRunState.Paused));
            Assert.That(factory.CreatedRunners[documents.Id].Status.State, Is.EqualTo(SyncPairRunState.Idle));
            Assert.That(factory.CreatedRunners[pictures.Id].Status.State, Is.EqualTo(SyncPairRunState.Idle));
            Assert.That(publisher.Current.SyncPairs.Select(status => status.State), Is.All.EqualTo(SyncPairRunState.Idle));
        });
    }

    [Test]
    public async Task SyncNowAsync_DelegatesToSelectedRunner()
    {
        SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
        SyncPairSettings pictures = CreatePair("Pictures", isEnabled: true);
        var factory = new FakeSyncPairRunnerFactory();
        var supervisor = new SyncSupervisor(
            new FakeSyncPairSettingsStore([documents, pictures]),
            factory,
            new InMemoryAppStatusPublisher());
        await supervisor.StartAsync();

        await supervisor.SyncNowAsync(pictures.Id);

        Assert.Multiple(() =>
        {
            Assert.That(factory.CreatedRunners[documents.Id].SyncNowCallCount, Is.Zero);
            Assert.That(factory.CreatedRunners[pictures.Id].SyncNowCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task StopAsync_StopsEveryRunnerAndPublishesDisabledStatuses()
    {
        SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
        SyncPairSettings pictures = CreatePair("Pictures", isEnabled: true);
        var factory = new FakeSyncPairRunnerFactory();
        var publisher = new InMemoryAppStatusPublisher();
        var supervisor = new SyncSupervisor(
            new FakeSyncPairSettingsStore([documents, pictures]),
            factory,
            publisher);
        await supervisor.StartAsync();

        await supervisor.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(factory.CreatedRunners.Values.Select(runner => runner.StopCallCount), Is.All.EqualTo(1));
            Assert.That(
                publisher.Current.SyncPairs.Select(status => status.State),
                Is.All.EqualTo(SyncPairRunState.Disabled));
        });
    }

    private static SyncPairSettings CreatePair(string displayName, bool isEnabled)
    {
        return new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            LocalRootPath = "/home/user/" + displayName,
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = "/" + displayName,
            IsEnabled = isEnabled,
            Mode = SyncPairMode.FullMirror,
        };
    }

    private sealed class FakeSyncPairSettingsStore : ISyncPairSettingsStore
    {
        private readonly IReadOnlyList<SyncPairSettings> _syncPairs;

        public FakeSyncPairSettingsStore(IReadOnlyList<SyncPairSettings> syncPairs)
        {
            _syncPairs = syncPairs;
        }

        public int InitializeCallCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncPairSettings>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_syncPairs);
        }

        public Task<SyncPairSettings?> GetAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_syncPairs.SingleOrDefault(syncPair => syncPair.Id == syncPairId));
        }

        public Task UpsertAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSyncPairRunnerFactory : ISyncPairRunnerFactory
    {
        public Dictionary<Guid, FakeSyncPairRunner> CreatedRunners { get; } = [];

        public List<FakeSyncPairRunner> AllCreatedRunners { get; } = [];

        public ISyncPairRunner Create(SyncPairSettings syncPair)
        {
            var runner = new FakeSyncPairRunner(syncPair);
            CreatedRunners[syncPair.Id] = runner;
            AllCreatedRunners.Add(runner);
            return runner;
        }
    }

    private sealed class FakeSyncPairRunner : ISyncPairRunner
    {
        private readonly SyncPairSettings _syncPair;
        private SyncPairRunState _state;

        public FakeSyncPairRunner(SyncPairSettings syncPair)
        {
            _syncPair = syncPair;
            _state = syncPair.IsEnabled ? SyncPairRunState.Idle : SyncPairRunState.Disabled;
        }

        public int PauseCallCount { get; private set; }

        public int ResumeCallCount { get; private set; }

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public int SyncNowCallCount { get; private set; }

        public Guid SyncPairId => _syncPair.Id;

        public SyncPairStatus Status => new(
            _syncPair.Id,
            _syncPair.DisplayName,
            _state,
            null,
            null,
            DateTime.UtcNow);

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            _state = _syncPair.IsEnabled ? SyncPairRunState.Idle : SyncPairRunState.Disabled;
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            PauseCallCount++;
            _state = SyncPairRunState.Paused;
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            ResumeCallCount++;
            _state = _syncPair.IsEnabled ? SyncPairRunState.Idle : SyncPairRunState.Disabled;
            return Task.CompletedTask;
        }

        public Task SyncNowAsync(CancellationToken cancellationToken = default)
        {
            SyncNowCallCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            _state = SyncPairRunState.Disabled;
            return Task.CompletedTask;
        }
    }
}
