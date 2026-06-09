// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Continuous;
using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.Tests.TestSupport;

namespace Cotton.Sync.App.Tests.Auth
{

    public sealed class SessionRevocationHandlerTests
    {
        [Test]
        public async Task HandleSessionRevokedAsync_StopsBackgroundWorkAndSignsOut()
        {
            var authFlow = new FakeAuthFlow();
            var localChanges = new FakeLocalChangeSyncCoordinator();
            var periodicSync = new FakePeriodicSyncCoordinator();
            var supervisor = new FakeSyncSupervisor();
            var handler = new SessionRevocationHandler(authFlow, localChanges, periodicSync, supervisor);

            await handler.HandleSessionRevokedAsync();

            Assert.Multiple(() =>
            {
                Assert.That(periodicSync.StopCallCount, Is.EqualTo(1));
                Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
                Assert.That(authFlow.SignOutCallCount, Is.EqualTo(1));
                Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task HandleSessionRevokedAsync_ContinuesShutdownWhenSignOutFails()
        {
            var authFlow = new FakeAuthFlow
            {
                SignOutException = new InvalidOperationException("logout failed"),
            };
            var localChanges = new FakeLocalChangeSyncCoordinator();
            var periodicSync = new FakePeriodicSyncCoordinator();
            var supervisor = new FakeSyncSupervisor();
            var handler = new SessionRevocationHandler(authFlow, localChanges, periodicSync, supervisor);

            await handler.HandleSessionRevokedAsync();

            Assert.Multiple(() =>
            {
                Assert.That(periodicSync.StopCallCount, Is.EqualTo(1));
                Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
                Assert.That(authFlow.SignOutCallCount, Is.EqualTo(1));
                Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task HandleSessionRevokedAsync_PublishesSessionRevocationEvent()
        {
            var authFlow = new FakeAuthFlow();
            var localChanges = new FakeLocalChangeSyncCoordinator();
            var periodicSync = new FakePeriodicSyncCoordinator();
            var supervisor = new FakeSyncSupervisor();
            var publisher = new InMemorySessionRevocationPublisher();
            var observer = new RecordingObserver<SessionRevocationEvent>();
            using IDisposable subscription = publisher.Subscribe(observer);
            var handler = new SessionRevocationHandler(
                authFlow,
                localChanges,
                periodicSync,
                supervisor,
                publisher);

            await handler.HandleSessionRevokedAsync();

            Assert.Multiple(() =>
            {
                Assert.That(observer.Values, Has.Count.EqualTo(1));
                Assert.That(observer.Values[0].OccurredAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            });
        }

        private sealed class FakeAuthFlow : IAuthFlow
        {
            public Exception? SignOutException { get; init; }

            public int SignOutCallCount { get; private set; }

            public Task<AuthSession> RestoreSessionAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<AuthSession> SignInAsync(
                PasswordSignInRequest request,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task SignOutAsync(CancellationToken cancellationToken = default)
            {
                SignOutCallCount++;
                return SignOutException is null
                    ? Task.CompletedTask
                    : Task.FromException(SignOutException);
            }
        }

        private sealed class FakeLocalChangeSyncCoordinator : ILocalChangeSyncCoordinator
        {
            public int StopCallCount { get; private set; }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakePeriodicSyncCoordinator : IPeriodicSyncCoordinator
        {
            public int StopCallCount { get; private set; }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeSyncSupervisor : ISyncSupervisor
        {
            public IReadOnlyList<SyncPairStatus> CurrentStatuses => [];

            public int StopCallCount { get; private set; }

            public Task PauseAllAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task PauseAsync(Guid syncPairId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task ResumeAllAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task StartAsync(bool startPaused, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                return Task.CompletedTask;
            }

            public Task SyncAllAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
