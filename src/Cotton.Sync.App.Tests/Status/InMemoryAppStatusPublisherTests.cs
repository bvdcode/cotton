// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Status;

namespace Cotton.Sync.App.Tests.Status
{
    public class InMemoryAppStatusPublisherTests
    {
        [Test]
        public void Subscribe_ReplaysCurrentStatus()
        {
            SyncAppStatus initialStatus = CreateStatus(isAuthenticated: true);
            var publisher = new InMemoryAppStatusPublisher(initialStatus);
            var observer = new RecordingStatusObserver();

            using IDisposable subscription = publisher.Subscribe(observer);

            Assert.That(observer.Values.Single(), Is.SameAs(initialStatus));
        }

        [Test]
        public void Publish_UpdatesCurrentAndNotifiesSubscribers()
        {
            var publisher = new InMemoryAppStatusPublisher();
            var observer = new RecordingStatusObserver();
            using IDisposable subscription = publisher.Subscribe(observer);
            SyncAppStatus nextStatus = CreateStatus(isAuthenticated: true);

            publisher.Publish(nextStatus);

            Assert.Multiple(() =>
            {
                Assert.That(publisher.Current, Is.SameAs(nextStatus));
                Assert.That(observer.Values.Last(), Is.SameAs(nextStatus));
                Assert.That(observer.Values, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void Dispose_RemovesSubscriber()
        {
            var publisher = new InMemoryAppStatusPublisher();
            var observer = new RecordingStatusObserver();
            IDisposable subscription = publisher.Subscribe(observer);
            subscription.Dispose();

            publisher.Publish(CreateStatus(isAuthenticated: true));

            Assert.That(observer.Values, Has.Count.EqualTo(1));
        }

        [Test]
        public void SyncPairStatus_NormalizesUpdatedAtToUtc()
        {
            DateTime localTime = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Local);

            var status = new SyncPairStatus(
                Guid.NewGuid(),
                "Documents",
                SyncPairRunState.Idle,
                null,
                null,
                localTime);

            Assert.That(status.UpdatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void SyncPairStatus_RejectsUnknownRunState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SyncPairStatus(
                    Guid.NewGuid(),
                    "Documents",
                    SyncPairRunState.Unknown,
                    null,
                    null,
                    DateTime.UtcNow));
        }

        [Test]
        public void SyncPairStatus_NormalizesLastSuccessfulSyncAtToUtc()
        {
            DateTime localTime = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Local);

            var status = new SyncPairStatus(
                Guid.NewGuid(),
                "Documents",
                SyncPairRunState.Idle,
                null,
                null,
                DateTime.UtcNow,
                localTime);

            Assert.That(status.LastSuccessfulSyncAtUtc?.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        private static SyncAppStatus CreateStatus(bool isAuthenticated)
        {
            return new SyncAppStatus(
                isAuthenticated,
                [
                    new SyncPairStatus(
                        Guid.NewGuid(),
                        "Documents",
                        SyncPairRunState.Idle,
                        null,
                        null,
                        new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc)),
                ],
                new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc));
        }

        private class RecordingStatusObserver : IObserver<SyncAppStatus>
        {
            public List<SyncAppStatus> Values { get; } = [];

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(SyncAppStatus value)
            {
                Values.Add(value);
            }
        }
    }
}
