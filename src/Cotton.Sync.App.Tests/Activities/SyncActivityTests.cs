// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;

namespace Cotton.Sync.App.Tests.Activities;

public sealed class SyncActivityTests
{
    [Test]
    public void Constructor_NormalizesOccurredAtToUtc()
    {
        DateTime unspecifiedTime = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Unspecified);

        var activity = new SyncActivity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SyncActivityType.Uploaded,
            "/Documents/report.txt",
            "Uploaded report.txt",
            unspecifiedTime);

        Assert.That(activity.OccurredAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
    }
}
