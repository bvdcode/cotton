// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Progress;
using CoreSyncTransferProgress = Cotton.Sync.SyncTransferProgress;

namespace Cotton.Sync.App.Runners
{
    internal class AppTransferProgressReporter : IProgress<CoreSyncTransferProgress>
    {
        private readonly AppTransferProgressEstimator _estimator = new();
        private readonly IAppTransferProgressPublisher _publisher;
        private readonly Guid _syncPairId;

        public AppTransferProgressReporter(Guid syncPairId, IAppTransferProgressPublisher publisher)
        {
            _syncPairId = syncPairId;
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        public void Report(CoreSyncTransferProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            AppTransferProgressEstimate estimate = _estimator.AddSample(
                value.Direction,
                value.RelativePath,
                value.TransferredBytes,
                value.TotalBytes,
                value.IsCompleted,
                value.OccurredAtUtc);
            _publisher.Publish(ToAppProgress(_syncPairId, value, estimate));
        }

        private static AppTransferProgress ToAppProgress(
            Guid syncPairId,
            CoreSyncTransferProgress progress,
            AppTransferProgressEstimate estimate)
        {
            return new AppTransferProgress(
                syncPairId,
                progress.Direction,
                progress.RelativePath,
                progress.TransferredBytes,
                progress.TotalBytes,
                progress.IsCompleted,
                progress.OccurredAtUtc,
                estimate.SpeedBytesPerSecond,
                estimate.EstimatedTimeRemaining);
        }
    }
}
