// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Progress;
using CoreSyncRunProgress = Cotton.Sync.SyncRunProgress;

namespace Cotton.Sync.App.Runners
{
    internal class AppRunProgressReporter : IProgress<CoreSyncRunProgress>
    {
        private readonly IAppRunProgressPublisher _publisher;
        private readonly Guid _syncPairId;

        public AppRunProgressReporter(Guid syncPairId, IAppRunProgressPublisher publisher)
        {
            _syncPairId = syncPairId;
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        public void Report(CoreSyncRunProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            _publisher.Publish(ToAppRunProgress(_syncPairId, value));
        }

        private static AppRunProgress ToAppRunProgress(
            Guid syncPairId,
            CoreSyncRunProgress progress)
        {
            return new AppRunProgress(
                syncPairId,
                progress.Stage,
                progress.FilesCompleted,
                progress.FilesTotal,
                progress.CurrentPath,
                progress.StartedAtUtc,
                progress.IsCompleted,
                progress.OccurredAtUtc,
                progress.BytesCompleted,
                progress.BytesTotal);
        }
    }
}
