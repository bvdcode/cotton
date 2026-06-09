// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;
using CoreSyncActivity = Cotton.Sync.SyncActivity;
using CoreSyncActivityKind = Cotton.Sync.SyncActivityKind;

namespace Cotton.Sync.App.Runners
{
    internal class AppActivityProgressReporter : IProgress<CoreSyncActivity>
    {
        private readonly IAppActivityPublisher _publisher;
        private readonly Guid _syncPairId;

        public AppActivityProgressReporter(Guid syncPairId, IAppActivityPublisher publisher)
        {
            _syncPairId = syncPairId;
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        public void Report(CoreSyncActivity value)
        {
            ArgumentNullException.ThrowIfNull(value);
            _publisher.Publish(ToAppActivity(_syncPairId, value));
        }

        private static AppSyncActivity ToAppActivity(
            Guid syncPairId,
            CoreSyncActivity activity)
        {
            string relativePath = activity.RelativePath.Trim();
            string message = CreateMessage(activity.Kind, relativePath, activity.Details);
            return new AppSyncActivity(
                Guid.NewGuid(),
                syncPairId,
                activity.Kind,
                string.IsNullOrWhiteSpace(relativePath) ? null : relativePath,
                message,
                DateTime.UtcNow);
        }

        private static string CreateMessage(CoreSyncActivityKind kind, string relativePath, string? details)
        {
            string item = string.IsNullOrWhiteSpace(relativePath) ? "item" : relativePath;
            string action = kind switch
            {
                CoreSyncActivityKind.Uploaded => "Uploaded",
                CoreSyncActivityKind.Downloaded => "Downloaded",
                CoreSyncActivityKind.DeletedLocal => "Deleted local copy",
                CoreSyncActivityKind.DeletedRemote => "Deleted remote copy",
                CoreSyncActivityKind.Conflict => "Created conflict copy",
                CoreSyncActivityKind.Skipped => "Skipped",
                _ => "Processed",
            };
            string message = action + " " + item;
            return string.IsNullOrWhiteSpace(details)
                ? message
                : message + ": " + details.Trim();
        }
    }
}
