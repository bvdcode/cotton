// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Continuous;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;

namespace Cotton.Sync.App.Tests.Continuous
{

    public sealed class PeriodicSyncCoordinatorTests
    {
        [Test]
        public async Task StartAsync_RequestsImmediateSyncAllByDefault()
        {
            var supervisor = new FakeSyncSupervisor();
            var coordinator = new PeriodicSyncCoordinator(supervisor, TimeSpan.FromMinutes(1));

            await coordinator.StartAsync();
            bool observed = await supervisor.WaitForSyncAsync(TimeSpan.FromSeconds(2));
            await coordinator.StopAsync();

            Assert.Multiple(() =>
            {
                Assert.That(observed, Is.True);
                Assert.That(supervisor.SyncAllCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task PeriodicTick_RequestsSyncAll()
        {
            var supervisor = new FakeSyncSupervisor();
            var coordinator = new PeriodicSyncCoordinator(
                supervisor,
                TimeSpan.FromMilliseconds(25),
                runImmediately: false);

            await coordinator.StartAsync();
            bool observed = await supervisor.WaitForSyncAsync(TimeSpan.FromSeconds(2));
            await coordinator.StopAsync();

            Assert.Multiple(() =>
            {
                Assert.That(observed, Is.True);
                Assert.That(supervisor.SyncAllCallCount, Is.GreaterThanOrEqualTo(1));
            });
        }

        [Test]
        public async Task StopAsync_CancelsPeriodicRequests()
        {
            var supervisor = new FakeSyncSupervisor();
            var coordinator = new PeriodicSyncCoordinator(
                supervisor,
                TimeSpan.FromMilliseconds(100),
                runImmediately: false);

            await coordinator.StartAsync();
            await coordinator.StopAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(150));

            Assert.That(supervisor.SyncAllCallCount, Is.Zero);
        }

        private sealed class FakeSyncSupervisor : ISyncSupervisor
        {
            private readonly TaskCompletionSource _syncRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public IReadOnlyList<SyncPairStatus> CurrentStatuses => [];

            public int SyncAllCallCount { get; private set; }

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

            public Task StartAsync(bool startPaused, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task SyncAllAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SyncAllCallCount++;
                _syncRequested.TrySetResult();
                return Task.CompletedTask;
            }

            public Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public async Task<bool> WaitForSyncAsync(TimeSpan timeout)
            {
                Task completed = await Task.WhenAny(_syncRequested.Task, Task.Delay(timeout)).ConfigureAwait(false);
                return completed == _syncRequested.Task;
            }
        }
    }
}
