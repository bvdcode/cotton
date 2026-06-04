// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sdk.Realtime;
using Cotton.Sync.App.RemoteChanges;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;

namespace Cotton.Sync.App.Tests.RemoteChanges;

public sealed class RealtimeRemoteChangeSyncCoordinatorTests
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(25);

    [Test]
    public async Task RemoteChanges_AreCoalescedIntoOneSyncAllRequest()
    {
        var realtime = new FakeCottonRealtimeClient();
        var supervisor = new FakeSyncSupervisor();
        var coordinator = new RealtimeRemoteChangeSyncCoordinator(realtime, supervisor, DebounceInterval);
        await coordinator.StartAsync();

        realtime.RaiseRemoteFileTreeChanged("FileCreated");
        realtime.RaiseRemoteFileTreeChanged("FileUpdated");

        bool observed = await supervisor.WaitForSyncAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(DebounceInterval * 3);
        await coordinator.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.True);
            Assert.That(supervisor.SyncAllCallCount, Is.EqualTo(1));
            Assert.That(realtime.StartCallCount, Is.EqualTo(1));
            Assert.That(realtime.StopCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task StopAsync_CancelsPendingSyncRequest()
    {
        var realtime = new FakeCottonRealtimeClient();
        var supervisor = new FakeSyncSupervisor();
        var coordinator = new RealtimeRemoteChangeSyncCoordinator(
            realtime,
            supervisor,
            TimeSpan.FromMilliseconds(100));
        await coordinator.StartAsync();

        realtime.RaiseRemoteFileTreeChanged("FileCreated");
        await coordinator.StopAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(150));

        Assert.That(supervisor.SyncAllCallCount, Is.Zero);
    }

    [Test]
    public async Task StopAsync_UnsubscribesFromRealtimeEvents()
    {
        var realtime = new FakeCottonRealtimeClient();
        var supervisor = new FakeSyncSupervisor();
        var sessionRevocationHandler = new FakeSessionRevocationHandler();
        var coordinator = new RealtimeRemoteChangeSyncCoordinator(
            realtime,
            supervisor,
            DebounceInterval,
            sessionRevocationHandler);
        await coordinator.StartAsync();
        await coordinator.StopAsync();

        realtime.RaiseRemoteFileTreeChanged("FileCreated");
        realtime.RaiseSessionRevoked();
        await Task.Delay(DebounceInterval * 3);

        Assert.Multiple(() =>
        {
            Assert.That(supervisor.SyncAllCallCount, Is.Zero);
            Assert.That(sessionRevocationHandler.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task StartAsync_UnsubscribesWhenRealtimeStartFails()
    {
        var realtime = new FakeCottonRealtimeClient
        {
            StartException = new InvalidOperationException("Realtime failed to start."),
        };
        var supervisor = new FakeSyncSupervisor();
        var sessionRevocationHandler = new FakeSessionRevocationHandler();
        var coordinator = new RealtimeRemoteChangeSyncCoordinator(
            realtime,
            supervisor,
            DebounceInterval,
            sessionRevocationHandler);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await coordinator.StartAsync());

        realtime.RaiseRemoteFileTreeChanged("FileCreated");
        realtime.RaiseSessionRevoked();
        await Task.Delay(DebounceInterval * 3);

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Is.EqualTo("Realtime failed to start."));
            Assert.That(realtime.StartCallCount, Is.EqualTo(1));
            Assert.That(realtime.StopCallCount, Is.EqualTo(1));
            Assert.That(supervisor.SyncAllCallCount, Is.Zero);
            Assert.That(sessionRevocationHandler.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task SessionRevoked_InvokesHandlerAndStopsRealtime()
    {
        var realtime = new FakeCottonRealtimeClient();
        var supervisor = new FakeSyncSupervisor();
        var sessionRevocationHandler = new FakeSessionRevocationHandler();
        var coordinator = new RealtimeRemoteChangeSyncCoordinator(
            realtime,
            supervisor,
            DebounceInterval,
            sessionRevocationHandler);
        await coordinator.StartAsync();

        realtime.RaiseSessionRevoked();

        bool handled = await sessionRevocationHandler.WaitForCallAsync(TimeSpan.FromSeconds(2));
        bool stopped = await realtime.WaitForStopAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(stopped, Is.True);
            Assert.That(sessionRevocationHandler.CallCount, Is.EqualTo(1));
            Assert.That(supervisor.SyncAllCallCount, Is.Zero);
            Assert.That(realtime.StopCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SessionRevoked_CancelsPendingRemoteSyncRequest()
    {
        var realtime = new FakeCottonRealtimeClient();
        var supervisor = new FakeSyncSupervisor();
        var sessionRevocationHandler = new FakeSessionRevocationHandler();
        var coordinator = new RealtimeRemoteChangeSyncCoordinator(
            realtime,
            supervisor,
            TimeSpan.FromMilliseconds(100),
            sessionRevocationHandler);
        await coordinator.StartAsync();

        realtime.RaiseRemoteFileTreeChanged("FileUpdated");
        realtime.RaiseSessionRevoked();

        bool handled = await sessionRevocationHandler.WaitForCallAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromMilliseconds(150));

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(supervisor.SyncAllCallCount, Is.Zero);
        });
    }

    private sealed class FakeCottonRealtimeClient : ICottonRealtimeClient
    {
        public event EventHandler<CottonRealtimeEvent>? RemoteFileTreeChanged;

        public event EventHandler<CottonRealtimeEvent>? SessionRevoked;

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public Exception? StartException { get; set; }

        private TaskCompletionSource StopRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void RaiseRemoteFileTreeChanged(string methodName)
        {
            RemoteFileTreeChanged?.Invoke(
                this,
                new CottonRealtimeEvent(
                    CottonRealtimeEventKind.RemoteFileTreeChanged,
                    methodName,
                    DateTime.UtcNow));
        }

        public void RaiseSessionRevoked()
        {
            SessionRevoked?.Invoke(
                this,
                new CottonRealtimeEvent(
                    CottonRealtimeEventKind.SessionRevoked,
                    CottonRealtimeHubMethods.SessionRevoked,
                    DateTime.UtcNow));
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCallCount++;
            if (StartException is not null)
            {
                throw StartException;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            StopRequested.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task<bool> WaitForStopAsync(TimeSpan timeout)
        {
            Task completed = await Task.WhenAny(StopRequested.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return completed == StopRequested.Task;
        }
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

    private sealed class FakeSessionRevocationHandler : ISessionRevocationHandler
    {
        private readonly TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public Task HandleSessionRevokedAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            _called.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task<bool> WaitForCallAsync(TimeSpan timeout)
        {
            Task completed = await Task.WhenAny(_called.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return completed == _called.Task;
        }
    }
}
