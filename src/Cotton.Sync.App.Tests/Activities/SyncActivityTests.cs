// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using AppSyncActivity = Cotton.Sync.App.Activities.SyncActivity;
using AppSyncActivityType = Cotton.Sync.App.Activities.SyncActivityType;

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
}
