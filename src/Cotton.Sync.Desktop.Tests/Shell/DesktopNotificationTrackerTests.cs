// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopNotificationTrackerTests
{
    [Test]
    public void Apply_EmitsInitialSyncCompleteWhenPairBecomesIdleAfterSync()
    {
        Guid syncPairId = Guid.NewGuid();
        var tracker = new DesktopNotificationTracker();
        _ = tracker.Apply(CreateStatus(syncPairId, "Syncing"), DisplayNames(syncPairId));

        IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
            CreateStatus(syncPairId, "Idle"),
            DisplayNames(syncPairId));

        Assert.Multiple(() =>
        {
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0].Kind, Is.EqualTo(DesktopNotificationKind.InitialSyncComplete));
            Assert.That(notifications[0].Message, Does.Contain("Documents"));
        });
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
    public void Reset_AllowsInitialSyncCompleteNotificationAgain()
    {
        Guid syncPairId = Guid.NewGuid();
        var tracker = new DesktopNotificationTracker();
        _ = tracker.Apply(CreateStatus(syncPairId, "Syncing"), DisplayNames(syncPairId));
        _ = tracker.Apply(CreateStatus(syncPairId, "Idle"), DisplayNames(syncPairId));

        tracker.Reset();
        _ = tracker.Apply(CreateStatus(syncPairId, "Syncing"), DisplayNames(syncPairId));
        IReadOnlyList<DesktopNotificationRequest> notifications = tracker.Apply(
            CreateStatus(syncPairId, "Idle"),
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
        string? lastError = null)
    {
        return new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, status, lastError),
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
