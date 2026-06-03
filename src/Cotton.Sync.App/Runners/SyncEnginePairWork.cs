// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;
using Cotton.Sync.App.SyncPairs;
using AppSyncActivity = Cotton.Sync.App.Activities.SyncActivity;
using AppSyncActivityType = Cotton.Sync.App.Activities.SyncActivityType;
using CoreSyncActivity = Cotton.Sync.SyncActivity;
using CoreSyncActivityKind = Cotton.Sync.SyncActivityKind;
using CoreSyncEngine = Cotton.Sync.ISyncEngine;
using CoreSyncPair = Cotton.Sync.SyncPair;
using CoreSyncRunOptions = Cotton.Sync.SyncRunOptions;

namespace Cotton.Sync.App.Runners;

/// <summary>
/// Runs sync pair work through the headless Cotton sync engine.
/// </summary>
public sealed class SyncEnginePairWork : ISyncPairWork
{
    private readonly IAppActivityPublisher? _activityPublisher;
    private readonly CoreSyncEngine _syncEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEnginePairWork" /> class.
    /// </summary>
    public SyncEnginePairWork(CoreSyncEngine syncEngine, IAppActivityPublisher? activityPublisher = null)
    {
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
        _activityPublisher = activityPublisher;
    }

    /// <inheritdoc />
    public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncPair);
        CoreSyncRunOptions? options = _activityPublisher is null
            ? null
            : new CoreSyncRunOptions
            {
                ActivityProgress = new AppActivityProgress(syncPair.Id, _activityPublisher),
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
}
