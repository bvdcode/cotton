// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sdk;
using Cotton.Sync.Local;

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
            Assert.That(runner.Status.LastSuccessfulSyncAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task StartAsync_DoesNotMarkPairAsSuccessfullySynced()
    {
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true));

        await runner.StartAsync();

        Assert.That(runner.Status.LastSuccessfulSyncAtUtc, Is.Null);
    }

    [Test]
    public async Task SyncNowAsync_ExposesCurrentOperationWhileWorkRuns()
    {
        var work = new BlockingSyncPairWork();
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        Task syncTask = runner.SyncNowAsync();
        await work.WaitForRunAsync(TimeSpan.FromSeconds(2));

        SyncPairStatus runningStatus = runner.Status;
        work.ReleaseCurrentRun();
        await syncTask;

        Assert.Multiple(() =>
        {
            Assert.That(runningStatus.State, Is.EqualTo(SyncPairRunState.Syncing));
            Assert.That(runningStatus.CurrentOperation, Is.EqualTo("Syncing changes"));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
            Assert.That(runner.Status.CurrentOperation, Is.Null);
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
    public async Task PauseAsync_ClearsQueuedSyncRequest()
    {
        var work = new BlockingFirstRunSyncPairWork();
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        Task firstSync = runner.SyncNowAsync();
        await work.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await runner.SyncNowAsync();
        Task pause = runner.PauseAsync();
        work.ReleaseRun();

        await Task.WhenAll(firstSync, pause).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(work.RunCount, Is.EqualTo(1));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Paused));
        });
    }

    [Test]
    public async Task StopAsync_ClearsQueuedSyncRequest()
    {
        var work = new BlockingFirstRunSyncPairWork();
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        Task firstSync = runner.SyncNowAsync();
        await work.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await runner.SyncNowAsync();
        Task stop = runner.StopAsync();
        work.ReleaseRun();

        await Task.WhenAll(firstSync, stop).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(work.RunCount, Is.EqualTo(1));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Disabled));
        });
    }

    [Test]
    public async Task PauseAsync_WhenCanceledBeforeStateChange_DoesNotBlockFutureSyncRequests()
    {
        var work = new BlockingFirstRunSyncPairWork();
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);
        using var cancellation = new CancellationTokenSource();

        Task firstSync = runner.SyncNowAsync();
        await work.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();

        OperationCanceledException? exception = Assert.CatchAsync<OperationCanceledException>(
            async () => await runner.PauseAsync(cancellation.Token));
        work.ReleaseRun();
        await firstSync.WaitAsync(TimeSpan.FromSeconds(2));
        await runner.SyncNowAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(work.RunCount, Is.EqualTo(2));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
        });
    }

    [Test]
    public async Task StopAsync_WhenCanceledBeforeStateChange_DoesNotBlockFutureSyncRequests()
    {
        var work = new BlockingFirstRunSyncPairWork();
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);
        using var cancellation = new CancellationTokenSource();

        Task firstSync = runner.SyncNowAsync();
        await work.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();

        OperationCanceledException? exception = Assert.CatchAsync<OperationCanceledException>(
            async () => await runner.StopAsync(cancellation.Token));
        work.ReleaseRun();
        await firstSync.WaitAsync(TimeSpan.FromSeconds(2));
        await runner.SyncNowAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(work.RunCount, Is.EqualTo(2));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
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
    public void SyncNowAsync_ReportsRemoteQuotaAsActionRequiredMessage()
    {
        var work = new FakeSyncPairWork
        {
            Failure = new CottonApiException(
                (System.Net.HttpStatusCode)507,
                null,
                "Cotton API request failed with status 507."),
        };
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        CottonApiException? exception = Assert.ThrowsAsync<CottonApiException>(
            async () => await runner.SyncNowAsync());

        const string expected = "Remote storage quota exceeded. Free space in Cotton Cloud or choose a smaller sync folder.";
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Error));
            Assert.That(runner.Status.LastError, Is.EqualTo(expected));
            Assert.That(runner.Status.CurrentOperation, Is.EqualTo("Action required: " + expected));
        });
    }

    [Test]
    public void SyncNowAsync_ReportsLocalPermissionDeniedAsActionRequiredMessage()
    {
        var work = new FakeSyncPairWork
        {
            Failure = new UnauthorizedAccessException("Access to the path was denied."),
        };
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        UnauthorizedAccessException? exception = Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await runner.SyncNowAsync());

        const string expected = "Permission denied while accessing local sync files. Check folder permissions and retry.";
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Error));
            Assert.That(runner.Status.LastError, Is.EqualTo(expected));
            Assert.That(runner.Status.CurrentOperation, Is.EqualTo("Action required: " + expected));
        });
    }

    [Test]
    public void SyncNowAsync_ReportsLocalDiskFullAsActionRequiredMessage()
    {
        var work = new FakeSyncPairWork
        {
            Failure = new TestIOException(unchecked((int)0x80070070)),
        };
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true), work);

        IOException? exception = Assert.ThrowsAsync<TestIOException>(
            async () => await runner.SyncNowAsync());

        const string expected = "Local disk is full. Free space on this computer and retry sync.";
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Error));
            Assert.That(runner.Status.LastError, Is.EqualTo(expected));
            Assert.That(runner.Status.CurrentOperation, Is.EqualTo("Action required: " + expected));
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
    public async Task SyncNowAsync_RetriesUnavailableLocalFileAndReturnsIdleOnRecovery()
    {
        var work = new FakeSyncPairWork
        {
            Failures =
            [
                new LocalFileUnavailableException(
                    "writing.txt",
                    "/home/user/Cotton/writing.txt",
                    "the file changed during scanning."),
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
    public async Task SyncNowAsync_RetriesLockedLocalFileAfterItBecomesReadable()
    {
        string root = Path.Combine(Path.GetTempPath(), "cotton-sync-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string filePath = Path.Combine(root, "locked.txt");
        File.WriteAllText(filePath, "locked");
        FileStream? locked = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var work = new ReleasingLockedFileSyncPairWork(() =>
        {
            locked?.Dispose();
            locked = null;
        });
        SyncPairRunner runner = CreateRunner(CreatePair(isEnabled: true, root), work, NoDelayRetryOptions());

        try
        {
            await runner.SyncNowAsync();

            Assert.Multiple(() =>
            {
                Assert.That(work.RunCount, Is.EqualTo(2));
                Assert.That(work.ScannedPaths, Is.EqualTo(new[] { "locked.txt" }));
                Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
            });
        }
        finally
        {
            locked?.Dispose();
            Directory.Delete(root, recursive: true);
        }
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

    private static SyncPairSettings CreatePair(bool isEnabled, string? localRootPath = null)
    {
        return new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Documents",
            LocalRootPath = localRootPath ?? "/home/user/Cotton",
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

    private sealed class ReleasingLockedFileSyncPairWork : ISyncPairWork
    {
        private readonly Action _releaseLock;
        private readonly LocalFileScanner _scanner = new();

        public ReleasingLockedFileSyncPairWork(Action releaseLock)
        {
            _releaseLock = releaseLock;
        }

        public int RunCount { get; private set; }

        public IReadOnlyList<string> ScannedPaths { get; private set; } = [];

        public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            RunCount++;
            try
            {
                IReadOnlyList<LocalFileSnapshot> files = await _scanner
                    .ScanAsync(syncPair.LocalRootPath, cancellationToken)
                    .ConfigureAwait(false);
                ScannedPaths = files.Select(file => file.RelativePath).ToList();
            }
            catch (LocalFileUnavailableException) when (RunCount == 1)
            {
                _releaseLock();
                throw;
            }
        }
    }

    private sealed class BlockingFirstRunSyncPairWork : ISyncPairWork
    {
        private readonly TaskCompletionSource _runStarted = CreateCompletionSource();
        private readonly TaskCompletionSource _releaseRun = CreateCompletionSource();

        public int RunCount { get; private set; }

        public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            RunCount++;
            _runStarted.TrySetResult();
            if (RunCount == 1)
            {
                await _releaseRun.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public void ReleaseRun()
        {
            _releaseRun.TrySetResult();
        }

        public Task WaitForRunAsync(TimeSpan timeout)
        {
            return _runStarted.Task.WaitAsync(timeout);
        }

        private static TaskCompletionSource CreateCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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

    private sealed class TestIOException : IOException
    {
        public TestIOException(int hresult)
            : base("Synthetic I/O failure.")
        {
            HResult = hresult;
        }
    }
}
