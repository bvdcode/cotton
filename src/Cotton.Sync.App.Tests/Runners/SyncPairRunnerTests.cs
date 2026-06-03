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

    private static SyncPairRunner CreateRunner(SyncPairSettings syncPair, ISyncPairWork? work = null)
    {
        return new SyncPairRunner(syncPair, work ?? new FakeSyncPairWork());
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
        public Exception? Failure { get; set; }

        public SyncPairSettings? LastSyncPair { get; private set; }

        public int RunCount { get; private set; }

        public Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            RunCount++;
            LastSyncPair = syncPair;
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.CompletedTask;
        }
    }
}
