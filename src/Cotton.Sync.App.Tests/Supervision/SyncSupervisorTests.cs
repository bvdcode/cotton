// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.Supervision
{
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
        public async Task StartAsync_StopsCreatedRunnersWhenLaterRunnerFails()
        {
            SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
            SyncPairSettings pictures = CreatePair("Pictures", isEnabled: true);
            var store = new FakeSyncPairSettingsStore([documents, pictures]);
            var factory = new FakeSyncPairRunnerFactory
            {
                FailingStartPairId = pictures.Id,
            };
            var publisher = new InMemoryAppStatusPublisher(new SyncAppStatus(true, [], DateTime.UtcNow));
            var supervisor = new SyncSupervisor(store, factory, publisher);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await supervisor.StartAsync());

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Message, Is.EqualTo("Runner failed to start."));
                Assert.That(factory.CreatedRunners[documents.Id].StopCallCount, Is.EqualTo(1));
                Assert.That(factory.CreatedRunners[pictures.Id].StopCallCount, Is.EqualTo(1));
                Assert.That(supervisor.CurrentStatuses, Is.Empty);
                Assert.That(publisher.Current.SyncPairs, Is.Empty);
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
                Assert.That(factory.CreatedRunners[documents.Id].SyncNowCallCount, Is.Zero);
                Assert.That(factory.CreatedRunners[pictures.Id].SyncNowCallCount, Is.EqualTo(1));
                Assert.That(publisher.Current.SyncPairs.Select(status => status.State), Is.All.EqualTo(SyncPairRunState.Idle));
            });
        }

        [Test]
        public async Task ResumeAllAsync_RequestsSyncForEnabledRunnersOnly()
        {
            SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
            SyncPairSettings pictures = CreatePair("Pictures", isEnabled: false);
            var factory = new FakeSyncPairRunnerFactory();
            var supervisor = new SyncSupervisor(
                new FakeSyncPairSettingsStore([documents, pictures]),
                factory,
                new InMemoryAppStatusPublisher());
            await supervisor.StartAsync();
            await supervisor.PauseAllAsync();

            await supervisor.ResumeAllAsync();

            Assert.Multiple(() =>
            {
                Assert.That(factory.CreatedRunners[documents.Id].ResumeCallCount, Is.EqualTo(1));
                Assert.That(factory.CreatedRunners[pictures.Id].ResumeCallCount, Is.EqualTo(1));
                Assert.That(factory.CreatedRunners[documents.Id].SyncNowCallCount, Is.EqualTo(1));
                Assert.That(factory.CreatedRunners[pictures.Id].SyncNowCallCount, Is.Zero);
            });
        }

        [Test]
        public async Task ResumeAsync_DoesNotBlockStopWhileResumeSyncIsRunning()
        {
            SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
            var factory = new FakeSyncPairRunnerFactory();
            var supervisor = new SyncSupervisor(
                new FakeSyncPairSettingsStore([documents]),
                factory,
                new InMemoryAppStatusPublisher());
            await supervisor.StartAsync();
            await supervisor.PauseAsync(documents.Id);
            FakeSyncPairRunner runner = factory.CreatedRunners[documents.Id];
            runner.BlockSyncNow = true;

            Task resume = supervisor.ResumeAsync(documents.Id);
            await runner.WaitForSyncNowAsync(TimeSpan.FromSeconds(2));
            Task stop = supervisor.StopAsync();
            bool stopReachedRunner = await runner.WaitForStopAsync(TimeSpan.FromMilliseconds(250));

            runner.ReleaseSyncNow();
            await Task.WhenAll(resume, stop).WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(stopReachedRunner, Is.True);
                Assert.That(runner.ResumeCallCount, Is.EqualTo(1));
                Assert.That(runner.SyncNowCallCount, Is.EqualTo(1));
                Assert.That(runner.StopCallCount, Is.EqualTo(1));
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
        public async Task SyncAllAsync_ContinuesOtherRunnersAndPublishesStatusWhenRunnerFails()
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
            factory.CreatedRunners[documents.Id].SyncNowException = new InvalidOperationException("Documents failed.");

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await supervisor.SyncAllAsync());

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Message, Is.EqualTo("Documents failed."));
                Assert.That(factory.CreatedRunners[documents.Id].SyncNowCallCount, Is.EqualTo(1));
                Assert.That(factory.CreatedRunners[pictures.Id].SyncNowCallCount, Is.EqualTo(1));
                Assert.That(
                    publisher.Current.SyncPairs.Select(status => status.State),
                    Is.EqualTo(new[] { SyncPairRunState.Error, SyncPairRunState.Idle }));
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

        [Test]
        public async Task StopAsync_ReachesRunnerWhileSyncAllIsRunning()
        {
            SyncPairSettings documents = CreatePair("Documents", isEnabled: true);
            var factory = new FakeSyncPairRunnerFactory();
            var supervisor = new SyncSupervisor(
                new FakeSyncPairSettingsStore([documents]),
                factory,
                new InMemoryAppStatusPublisher());
            await supervisor.StartAsync();
            FakeSyncPairRunner runner = factory.CreatedRunners[documents.Id];
            runner.BlockSyncNow = true;

            Task syncAll = supervisor.SyncAllAsync();
            await runner.WaitForSyncNowAsync(TimeSpan.FromSeconds(2));
            Task stop = supervisor.StopAsync();
            bool stopReachedRunner = await runner.WaitForStopAsync(TimeSpan.FromMilliseconds(250));
            runner.ReleaseSyncNow();
            await Task.WhenAll(syncAll, stop).WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(stopReachedRunner, Is.True);
                Assert.That(runner.SyncNowCallCount, Is.EqualTo(1));
                Assert.That(runner.StopCallCount, Is.EqualTo(1));
                Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Disabled));
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

            public Guid? FailingStartPairId { get; set; }

            public ISyncPairRunner Create(SyncPairSettings syncPair)
            {
                var runner = new FakeSyncPairRunner(syncPair);
                if (syncPair.Id == FailingStartPairId)
                {
                    runner.StartException = new InvalidOperationException("Runner failed to start.");
                }

                CreatedRunners[syncPair.Id] = runner;
                AllCreatedRunners.Add(runner);
                return runner;
            }
        }

        private sealed class FakeSyncPairRunner : ISyncPairRunner
        {
            private readonly SyncPairSettings _syncPair;
            private readonly TaskCompletionSource _stopStarted = CreateCompletionSource();
            private readonly TaskCompletionSource _syncNowRelease = CreateCompletionSource();
            private readonly TaskCompletionSource _syncNowStarted = CreateCompletionSource();
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

            public bool BlockSyncNow { get; set; }

            public Exception? StartException { get; set; }

            public Exception? SyncNowException { get; set; }

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
                if (StartException is not null)
                {
                    throw StartException;
                }

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

            public async Task SyncNowAsync(CancellationToken cancellationToken = default)
            {
                SyncNowCallCount++;
                _syncNowStarted.TrySetResult();
                if (SyncNowException is not null)
                {
                    _state = SyncPairRunState.Error;
                    throw SyncNowException;
                }

                if (BlockSyncNow)
                {
                    await _syncNowRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                _stopStarted.TrySetResult();
                _state = SyncPairRunState.Disabled;
                return Task.CompletedTask;
            }

            public void ReleaseSyncNow()
            {
                _syncNowRelease.TrySetResult();
            }

            public async Task<bool> WaitForStopAsync(TimeSpan timeout)
            {
                try
                {
                    await _stopStarted.Task.WaitAsync(timeout).ConfigureAwait(false);
                    return true;
                }
                catch (TimeoutException)
                {
                    return false;
                }
            }

            public Task WaitForSyncNowAsync(TimeSpan timeout)
            {
                return _syncNowStarted.Task.WaitAsync(timeout);
            }

            private static TaskCompletionSource CreateCompletionSource()
            {
                return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
