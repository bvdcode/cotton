// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;
using Cotton.Sync.App.Progress;
using Cotton.Sync.App.SyncPairs;
using AppSyncActivity = Cotton.Sync.App.Activities.SyncActivity;
using AppSyncActivityType = Cotton.Sync.App.Activities.SyncActivityType;
using CoreSyncActivity = Cotton.Sync.SyncActivity;
using CoreSyncActivityKind = Cotton.Sync.SyncActivityKind;
using CoreSyncEngine = Cotton.Sync.ISyncEngine;
using CoreSyncPair = Cotton.Sync.SyncPair;
using CoreSyncRunProgress = Cotton.Sync.SyncRunProgress;
using CoreSyncRunProgressStage = Cotton.Sync.SyncRunProgressStage;
using CoreSyncRunOptions = Cotton.Sync.SyncRunOptions;
using CoreSyncTransferDirection = Cotton.Sync.SyncTransferDirection;
using CoreSyncTransferProgress = Cotton.Sync.SyncTransferProgress;

namespace Cotton.Sync.App.Runners;

/// <summary>
/// Runs sync pair work through the headless Cotton sync engine.
/// </summary>
public sealed class SyncEnginePairWork : ISyncPairWork
{
    private readonly IAppActivityPublisher? _activityPublisher;
    private readonly IAppTransferProgressPublisher? _progressPublisher;
    private readonly IAppRunProgressPublisher? _runProgressPublisher;
    private readonly CoreSyncEngine _syncEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEnginePairWork" /> class.
    /// </summary>
    public SyncEnginePairWork(
        CoreSyncEngine syncEngine,
        IAppActivityPublisher? activityPublisher = null,
        IAppTransferProgressPublisher? progressPublisher = null,
        IAppRunProgressPublisher? runProgressPublisher = null)
    {
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
        _activityPublisher = activityPublisher;
        _progressPublisher = progressPublisher;
        _runProgressPublisher = runProgressPublisher;
    }

    /// <inheritdoc />
    public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncPair);
        CoreSyncRunOptions? options = _activityPublisher is null && _progressPublisher is null && _runProgressPublisher is null
            ? null
            : new CoreSyncRunOptions
            {
                ActivityProgress = _activityPublisher is null ? null : new AppActivityProgress(syncPair.Id, _activityPublisher),
                TransferProgress = _progressPublisher is null ? null : new AppTransferProgressReporter(syncPair.Id, _progressPublisher),
                RunProgress = _runProgressPublisher is null ? null : new AppRunProgressReporter(syncPair.Id, _runProgressPublisher),
            };
        _ = await _syncEngine
            .RunOnceAsync(ToCorePair(syncPair), options, cancellationToken)
            .ConfigureAwait(false);
    }

    private static CoreSyncPair ToCorePair(SyncPairSettings syncPair)
    {
        return new CoreSyncPair
        {
            SyncPairId = syncPair.Id.ToString("D"),
            LocalRootPath = syncPair.LocalRootPath,
            RemoteRootNodeId = syncPair.RemoteRootNodeId,
        };
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
            ToAppActivityType(activity.Kind),
            string.IsNullOrWhiteSpace(relativePath) ? null : relativePath,
            message,
            DateTime.UtcNow);
    }

    private static AppTransferProgress ToAppProgress(
        Guid syncPairId,
        CoreSyncTransferProgress progress,
        AppTransferProgressEstimate estimate)
    {
        return new AppTransferProgress(
            syncPairId,
            ToAppTransferDirection(progress.Direction),
            progress.RelativePath,
            progress.TransferredBytes,
            progress.TotalBytes,
            progress.IsCompleted,
            progress.OccurredAtUtc,
            estimate.SpeedBytesPerSecond,
            estimate.EstimatedTimeRemaining);
    }

    private static AppRunProgress ToAppRunProgress(
        Guid syncPairId,
        CoreSyncRunProgress progress)
    {
        return new AppRunProgress(
            syncPairId,
            ToAppRunProgressStage(progress.Stage),
            progress.FilesCompleted,
            progress.FilesTotal,
            progress.CurrentPath,
            progress.StartedAtUtc,
            progress.IsCompleted,
            progress.OccurredAtUtc);
    }

    private static AppTransferDirection ToAppTransferDirection(CoreSyncTransferDirection direction)
    {
        return direction switch
        {
            CoreSyncTransferDirection.Upload => AppTransferDirection.Upload,
            CoreSyncTransferDirection.Download => AppTransferDirection.Download,
            _ => AppTransferDirection.Unknown,
        };
    }

    private static AppRunProgressStage ToAppRunProgressStage(CoreSyncRunProgressStage stage)
    {
        return stage switch
        {
            CoreSyncRunProgressStage.ScanningLocal => AppRunProgressStage.ScanningLocal,
            CoreSyncRunProgressStage.ScanningRemote => AppRunProgressStage.ScanningRemote,
            CoreSyncRunProgressStage.ReconcilingDirectories => AppRunProgressStage.ReconcilingDirectories,
            CoreSyncRunProgressStage.ReconcilingFiles => AppRunProgressStage.ReconcilingFiles,
            CoreSyncRunProgressStage.Completed => AppRunProgressStage.Completed,
            _ => AppRunProgressStage.Unknown,
        };
    }

    private static AppSyncActivityType ToAppActivityType(CoreSyncActivityKind kind)
    {
        return kind switch
        {
            CoreSyncActivityKind.Uploaded => AppSyncActivityType.Uploaded,
            CoreSyncActivityKind.Downloaded => AppSyncActivityType.Downloaded,
            CoreSyncActivityKind.DeletedLocal => AppSyncActivityType.DeletedLocal,
            CoreSyncActivityKind.DeletedRemote => AppSyncActivityType.DeletedRemote,
            CoreSyncActivityKind.Conflict => AppSyncActivityType.Conflict,
            CoreSyncActivityKind.Skipped => AppSyncActivityType.Skipped,
            _ => AppSyncActivityType.Warning,
        };
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

    private sealed class AppActivityProgress : IProgress<CoreSyncActivity>
    {
        private readonly IAppActivityPublisher _publisher;
        private readonly Guid _syncPairId;

        public AppActivityProgress(Guid syncPairId, IAppActivityPublisher publisher)
        {
            _syncPairId = syncPairId;
            _publisher = publisher;
        }

        public void Report(CoreSyncActivity value)
        {
            ArgumentNullException.ThrowIfNull(value);
            _publisher.Publish(ToAppActivity(_syncPairId, value));
        }
    }

    private sealed class AppTransferProgressReporter : IProgress<CoreSyncTransferProgress>
    {
        private readonly AppTransferProgressEstimator _estimator = new();
        private readonly IAppTransferProgressPublisher _publisher;
        private readonly Guid _syncPairId;

        public AppTransferProgressReporter(Guid syncPairId, IAppTransferProgressPublisher publisher)
        {
            _syncPairId = syncPairId;
            _publisher = publisher;
        }

        public void Report(CoreSyncTransferProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            AppTransferDirection direction = ToAppTransferDirection(value.Direction);
            AppTransferProgressEstimate estimate = _estimator.AddSample(
                direction,
                value.RelativePath,
                value.TransferredBytes,
                value.TotalBytes,
                value.IsCompleted,
                value.OccurredAtUtc);
            _publisher.Publish(ToAppProgress(_syncPairId, value, estimate));
        }
    }

    private sealed class AppRunProgressReporter : IProgress<CoreSyncRunProgress>
    {
        private readonly IAppRunProgressPublisher _publisher;
        private readonly Guid _syncPairId;

        public AppRunProgressReporter(Guid syncPairId, IAppRunProgressPublisher publisher)
        {
            _syncPairId = syncPairId;
            _publisher = publisher;
        }

        public void Report(CoreSyncRunProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            _publisher.Publish(ToAppRunProgress(_syncPairId, value));
        }
    }
}
