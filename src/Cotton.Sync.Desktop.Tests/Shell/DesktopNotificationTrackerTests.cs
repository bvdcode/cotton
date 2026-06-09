// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell
{

    public sealed class DesktopNotificationTrackerTests
    {
        [Test]
        public void NotificationRequest_RejectsUnknownKind()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopNotificationRequest(
                DesktopNotificationKind.Unknown,
                Guid.NewGuid(),
                "Title",
                "Message"));
        }

        [Test]
        public void NotificationRequest_RejectsBlankTitleAndMessage()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => new DesktopNotificationRequest(
                    DesktopNotificationKind.Conflict,
                    Guid.NewGuid(),
                    string.Empty,
                    "Message"));
                Assert.Throws<ArgumentException>(() => new DesktopNotificationRequest(
                    DesktopNotificationKind.Conflict,
                    Guid.NewGuid(),
                    "Title",
                    " "));
            });
        }

        [Test]
        public void Apply_EmitsInitialSyncCompleteWhenPairBecomesIdleAfterSync()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();
            _ = tracker.Apply(CreateStatus(syncPairId, "Syncing"), DisplayNames(syncPairId));

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Idle", lastSyncedAtUtc: DateTime.UtcNow),
                DisplayNames(syncPairId));

            Assert.Multiple(() =>
            {
                Assert.That(notifications, Has.Count.EqualTo(1));
                Assert.That(notifications[0].Kind, Is.EqualTo(DesktopNotificationKind.InitialSyncComplete));
                Assert.That(notifications[0].Message, Does.Contain("Documents"));
            });
        }

        [Test]
        public void Apply_DoesNotEmitInitialSyncCompleteWithoutSuccessfulSyncTimestamp()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();
            _ = tracker.Apply(CreateStatus(syncPairId, "Syncing"), DisplayNames(syncPairId));

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Idle"),
                DisplayNames(syncPairId));

            Assert.That(notifications, Is.Empty);
        }

        [Test]
        public void Apply_EmitsConflictNotification()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Conflict"),
                DisplayNames(syncPairId));

            Assert.That(notifications.Single().Kind, Is.EqualTo(DesktopNotificationKind.Conflict));
        }

        [Test]
        public void Apply_EmitsActionRequiredErrorNotification()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Error", "Local folder is unavailable."),
                DisplayNames(syncPairId));

            Assert.Multiple(() =>
            {
                Assert.That(notifications.Single().Kind, Is.EqualTo(DesktopNotificationKind.ActionRequiredError));
                Assert.That(notifications.Single().Message, Does.Contain("Local folder is unavailable."));
            });
        }

        [Test]
        public void Apply_NormalizesActionRequiredErrorNotification()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();
            const string rawError = "Cotton API request GET /api/v1/sync/changes?since=0&limit=500 returned invalid JSON "
                + "with content type 'text/html' and status 200 (OK). Response: <!doctype html><html>App</html>";

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Error", rawError),
                DisplayNames(syncPairId));

            DesktopNotificationRequest notification = notifications.Single();
            Assert.Multiple(() =>
            {
                Assert.That(notification.Kind, Is.EqualTo(DesktopNotificationKind.ActionRequiredError));
                Assert.That(
                    notification.Message,
                    Is.EqualTo("Documents: This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync."));
                Assert.That(notification.Message, Does.Not.Contain("invalid JSON"));
                Assert.That(notification.Message, Does.Not.Contain("<!doctype html>"));
            });
        }

        [Test]
        public void Apply_NormalizesDiskFullActionRequiredErrorNotification()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Error", "No space left on device"),
                DisplayNames(syncPairId));

            DesktopNotificationRequest notification = notifications.Single();
            Assert.Multiple(() =>
            {
                Assert.That(notification.Kind, Is.EqualTo(DesktopNotificationKind.ActionRequiredError));
                Assert.That(
                    notification.Message,
                    Is.EqualTo("Documents: This computer does not have enough free disk space for sync. Free space and retry."));
            });
        }

        [Test]
        public void Apply_NormalizesEmbeddedProblemDetailsActionRequiredNotification()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();
            const string rawError =
                "Cotton API request POST /api/v1/files/from-chunks failed with status 400 (BadRequest). "
                + "Response: {\"type\":\"https://tools.ietf.org/html/rfc7231#section-6.5.1\","
                + "\"title\":\"Bad Request\",\"status\":400,\"detail\":\"Bad request\","
                + "\"instance\":\"/api/v1/files/from-chunks\",\"traceId\":\"00-test\"}";

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Error", rawError),
                DisplayNames(syncPairId));

            DesktopNotificationRequest notification = notifications.Single();
            Assert.Multiple(() =>
            {
                Assert.That(notification.Kind, Is.EqualTo(DesktopNotificationKind.ActionRequiredError));
                Assert.That(
                    notification.Message,
                    Is.EqualTo("Documents: Remote upload was rejected by Cotton Cloud. Check diagnostics and retry."));
                Assert.That(notification.Message, Does.Not.Contain("Response:"));
                Assert.That(notification.Message, Does.Not.Contain("traceId"));
            });
        }

        [Test]
        public void Apply_EmitsGenericActionRequiredErrorNotificationWhenDetailsAreMissing()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Error"),
                DisplayNames(syncPairId));

            Assert.Multiple(() =>
            {
                Assert.That(notifications.Single().Kind, Is.EqualTo(DesktopNotificationKind.ActionRequiredError));
                Assert.That(
                    notifications.Single().Message,
                    Is.EqualTo("Documents: One or more sync folders reported an error. Check diagnostics and retry."));
            });
        }

        [Test]
        public void Reset_AllowsInitialSyncCompleteNotificationAgain()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();
            _ = tracker.Apply(CreateStatus(syncPairId, "Syncing"), DisplayNames(syncPairId));
            _ = tracker.Apply(CreateStatus(syncPairId, "Idle", lastSyncedAtUtc: DateTime.UtcNow), DisplayNames(syncPairId));

            tracker.Reset();
            _ = tracker.Apply(CreateStatus(syncPairId, "Syncing"), DisplayNames(syncPairId));
            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Idle", lastSyncedAtUtc: DateTime.UtcNow),
                DisplayNames(syncPairId));

            Assert.That(notifications.Single().Kind, Is.EqualTo(DesktopNotificationKind.InitialSyncComplete));
        }

        [Test]
        public void Apply_DoesNotRepeatSameErrorNotification()
        {
            Guid syncPairId = Guid.NewGuid();
            var tracker = new DesktopNotificationTracker();
            _ = tracker.Apply(
                CreateStatus(syncPairId, "Error", "Local folder is unavailable."),
                DisplayNames(syncPairId));

            IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
                CreateStatus(syncPairId, "Error", "Local folder is unavailable."),
                DisplayNames(syncPairId));

            Assert.That(notifications, Is.Empty);
        }

        private static DesktopSyncStatusSnapshot CreateStatus(
            Guid syncPairId,
            string status,
            string? lastError = null,
            DateTime? lastSyncedAtUtc = null)
        {
            return new DesktopSyncStatusSnapshot(
            [
                new DesktopSyncPairStatusSnapshot(syncPairId, status, lastError, LastSyncedAtUtc: lastSyncedAtUtc),
            ]);
        }

        private static IReadOnlyDictionary<Guid, string> DisplayNames(Guid syncPairId)
        {
            return new Dictionary<Guid, string>
            {
                [syncPairId] = "Documents",
            };
        }
    }
}
