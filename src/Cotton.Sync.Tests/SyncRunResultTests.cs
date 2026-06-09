// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Tests
{

    public sealed class SyncRunResultTests
    {
        [Test]
        public void RecordActivity_CapsStoredActivitiesButKeepsActionRequiredSummary()
        {
            var result = new SyncRunResult();

            result.RecordActivity(
                new SyncActivity
                {
                    Kind = SyncActivityKind.Uploaded,
                    RelativePath = "Documents/first.txt",
                },
                maximumStoredActivities: 1);
            result.RecordActivity(
                new SyncActivity
                {
                    Kind = SyncActivityKind.Skipped,
                    RelativePath = "Documents/late-conflict.txt",
                    Details = "Conflict needs review.",
                    RequiresUserAction = true,
                },
                maximumStoredActivities: 1);

            Assert.Multiple(() =>
            {
                Assert.That(result.TotalActivityCount, Is.EqualTo(2));
                Assert.That(result.Activities, Has.Count.EqualTo(1));
                Assert.That(result.Activities.Single().RelativePath, Is.EqualTo("Documents/first.txt"));
                Assert.That(result.IsActivityListTruncated, Is.True);
                Assert.That(result.RequiresUserAction, Is.True);
                Assert.That(result.ActionRequiredMessage, Is.EqualTo("Conflict needs review."));
            });
        }
    }
}
