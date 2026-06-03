// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;
using AppSyncActivity = Cotton.Sync.App.Activities.SyncActivity;
using AppSyncActivityType = Cotton.Sync.App.Activities.SyncActivityType;
using Cotton.Sync.App.Tests.TestSupport;

namespace Cotton.Sync.App.Tests.Activities;

public sealed class SyncActivityTests
{
    [Test]
    public void Constructor_NormalizesOccurredAtToUtc()
    {
        DateTime unspecifiedTime = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Unspecified);

        var activity = new AppSyncActivity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppSyncActivityType.Uploaded,
            "/Documents/report.txt",
            "Uploaded report.txt",
            unspecifiedTime);

        Assert.That(activity.OccurredAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void InMemoryPublisher_PublishesActivitiesToSubscribers()
    {
        var publisher = new InMemoryAppActivityPublisher();
        var observer = new RecordingObserver<AppSyncActivity>();
        var activity = new AppSyncActivity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppSyncActivityType.Downloaded,
            "report.txt",
            "Downloaded report.txt",
            DateTime.UtcNow);

        using IDisposable subscription = publisher.Subscribe(observer);
        publisher.Publish(activity);

        Assert.That(observer.Values.Single(), Is.SameAs(activity));
    }

    [Test]
    public void InMemoryPublisher_StopsPublishingAfterUnsubscribe()
    {
        var publisher = new InMemoryAppActivityPublisher();
        var observer = new RecordingObserver<AppSyncActivity>();
        var activity = new AppSyncActivity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppSyncActivityType.Uploaded,
            "report.txt",
            "Uploaded report.txt",
            DateTime.UtcNow);

        IDisposable subscription = publisher.Subscribe(observer);
        subscription.Dispose();
        publisher.Publish(activity);

        Assert.That(observer.Values, Is.Empty);
    }

}
