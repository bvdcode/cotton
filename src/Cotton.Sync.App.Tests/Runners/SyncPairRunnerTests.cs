// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.Runners;

public sealed class SyncPairRunnerTests
{
    [Test]
    public async Task StartAsync_SetsIdleForEnabledPair()
    {
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true));

        await runner.StartAsync();

        Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
    }

    [Test]
    public async Task StartAsync_SetsDisabledForDisabledPair()
    {
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: false));

        await runner.StartAsync();

        Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Disabled));
    }

    [Test]
    public async Task PauseAndResumeAsync_UpdateState()
    {
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true));
        await runner.StartAsync();

        await runner.PauseAsync();
        SyncPairRunState pausedState = runner.Status.State;
        await runner.ResumeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pausedState, Is.EqualTo(SyncPairRunState.Paused));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
        });
    }

    [Test]
    public async Task SyncNowAsync_RunsWorkAndReturnsIdle()
    {
        var work = new FakeSyncPairWork();
        SyncPairSettings syncPair = CreatePair(isEnabled: true);
        SyncPairRunner runner = CreateRunner(syncPair, work);

        await runner.SyncNowAsync();

        Assert.Multiple(() =>
        {
            Assert.That(work.RunCount, Is.EqualTo(1));
            Assert.That(work.LastSyncPair, Is.SameAs(syncPair));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
        });
    }

    [Test]
    public async Task SyncNowAsync_DoesNotRunWhenPaused()
    {
        var work = new FakeSyncPairWork();
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);
        await runner.PauseAsync();

        await runner.SyncNowAsync();

        Assert.Multiple(() =>
        {
            Assert.That(work.RunCount, Is.Zero);
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Paused));
        });
    }

    [Test]
    public void SyncNowAsync_SetsErrorAndRethrowsOnFailure()
    {
        var work = new FakeSyncPairWork
        {
            Failure = new InvalidOperationException("sync failed"),
        };
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await runner.SyncNowAsync());

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Error));
            Assert.That(runner.Status.LastError, Is.EqualTo("sync failed"));
        });
    }

    [Test]
    public async Task SyncNowAsync_RetriesTransientNetworkFailureAndReturnsIdleOnRecovery()
    {
        var work = new FakeSyncPairWork
        {
            Failures =
            [
                new HttpRequestException("server unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable),
            ],
        };
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work, NoDelayRetryOptions());

        await runner.SyncNowAsync();

        Assert.Multiple(() =>
        {
            Assert.That(work.RunCount, Is.EqualTo(2));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
        });
    }

    [Test]
    public void SyncNowAsync_SetsOfflineAndRethrowsWhenTransientNetworkFailurePersists()
    {
        var work = new FakeSyncPairWork
        {
            Failures =
            [
                new HttpRequestException("network down"),
                new HttpRequestException("network down"),
            ],
        };
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work, NoDelayRetryOptions(maxAttempts: 2));

        HttpRequestException? exception = Assert.ThrowsAsync<HttpRequestException>(
            async () => await runner.SyncNowAsync());

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(work.RunCount, Is.EqualTo(2));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Offline));
            Assert.That(runner.Status.LastError, Is.EqualTo("network down"));
        });
    }

    [Test]
    public async Task SyncNowAsync_CoalescesOverlappingRequestsIntoOneQueuedRun()
    {
        var work = new BlockingSyncPairWork();
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        Task first = runner.SyncNowAsync();
        await work.WaitForRunAsync(TimeSpan.FromSeconds(2));
        Task second = runner.SyncNowAsync();
        Task third = runner.SyncNowAsync();

        await Task.WhenAll(second, third);
        work.ReleaseCurrentRun();
        await work.WaitForRunCountAsync(2, TimeSpan.FromSeconds(2));
        work.ReleaseCurrentRun();
        await first;

        Assert.That(work.RunCount, Is.EqualTo(2));
    }

    private static SyncPairRunner CreateRunner(
        SyncPairSettings syncPair,
        ISyncPairWork? work = null,
        SyncPairRunnerRetryOptions? retryOptions = null)
    {
        return new SyncPairRunner(syncPair, work ?? new FakeSyncPairWork(), retryOptions);
    }

    private static SyncPairRunnerRetryOptions NoDelayRetryOptions(int maxAttempts = 3)
    {
        return new SyncPairRunnerRetryOptions
        {
            MaxAttempts = maxAttempts,
            InitialDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero,
        };
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

    private sealed class FakeSyncPairWork : ISyncPairWork
    {
        private readonly Queue<Exception> _failures = [];

        public Exception? Failure { get; set; }

        public IReadOnlyList<Exception> Failures
        {
            set
            {
                _failures.Clear();
                foreach (Exception failure in value)
                {
                    _failures.Enqueue(failure);
                }
            }
        }

        public SyncPairSettings? LastSyncPair { get; private set; }

        public int RunCount { get; private set; }

        public Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            RunCount++;
            LastSyncPair = syncPair;
            if (_failures.Count > 0)
            {
                throw _failures.Dequeue();
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class BlockingSyncPairWork : ISyncPairWork
    {
        private readonly object _gate = new();
        private TaskCompletionSource _currentRunStarted = CreateCompletionSource();
        private TaskCompletionSource _currentRunRelease = CreateCompletionSource();
        private TaskCompletionSource _secondRunStarted = CreateCompletionSource();

        public int RunCount { get; private set; }

        public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource release;
            lock (_gate)
            {
                RunCount++;
                release = _currentRunRelease;
                _currentRunStarted.TrySetResult();
                if (RunCount >= 2)
                {
                    _secondRunStarted.TrySetResult();
                }
            }

            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public void ReleaseCurrentRun()
        {
            lock (_gate)
            {
                _currentRunRelease.TrySetResult();
                _currentRunStarted = CreateCompletionSource();
                _currentRunRelease = CreateCompletionSource();
            }
        }

        public Task WaitForRunAsync(TimeSpan timeout)
        {
            Task task;
            lock (_gate)
            {
                task = _currentRunStarted.Task;
            }

            return task.WaitAsync(timeout);
        }

        public async Task WaitForRunCountAsync(int runCount, TimeSpan timeout)
        {
            if (RunCount >= runCount)
            {
                return;
            }

            await _secondRunStarted.Task.WaitAsync(timeout).ConfigureAwait(false);
        }

        private static TaskCompletionSource CreateCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
