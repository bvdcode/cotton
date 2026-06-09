// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;
using Cotton.Sync.App.Progress;
using Cotton.Sync.App.SyncPairs;
using CoreSyncEngine = Cotton.Sync.ISyncEngine;
using CoreSyncPair = Cotton.Sync.SyncPair;
using CoreSyncRunOptions = Cotton.Sync.SyncRunOptions;
using CoreSyncRunResult = Cotton.Sync.SyncRunResult;

namespace Cotton.Sync.App.Runners
{
    /// <summary>
    /// Runs sync pair work through the headless Cotton sync engine.
    /// </summary>
    public class SyncEnginePairWork : ISyncPairWork
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
                    ActivityProgress = _activityPublisher is null ? null : new AppActivityProgressReporter(syncPair.Id, _activityPublisher),
                    TransferProgress = _progressPublisher is null ? null : new AppTransferProgressReporter(syncPair.Id, _progressPublisher),
                    RunProgress = _runProgressPublisher is null ? null : new AppRunProgressReporter(syncPair.Id, _runProgressPublisher),
                };
            CoreSyncRunResult result = await _syncEngine
                .RunOnceAsync(ToCorePair(syncPair), options, cancellationToken)
                .ConfigureAwait(false);
            if (result.RequiresUserAction)
            {
                throw new SyncActionRequiredException(CreateActionRequiredMessage(result));
            }
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

        private static string CreateActionRequiredMessage(CoreSyncRunResult result)
        {
            return string.IsNullOrWhiteSpace(result.ActionRequiredMessage)
                ? "Sync requires your attention before it can continue."
                : result.ActionRequiredMessage.Trim();
        }
    }
}
