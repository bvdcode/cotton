// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.Runners;
using Cotton.Sync.App.SyncPairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.LocalChanges
{
    /// <summary>
    /// Watches local sync roots and requests debounced sync passes.
    /// </summary>
    public class LocalChangeSyncCoordinator : ILocalChangeSyncCoordinator
    {
        private static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(750);

        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private readonly object _pendingGate = new();
        private readonly TimeSpan _debounceInterval;
        private readonly ILogger<LocalChangeSyncCoordinator> _logger;
        private readonly ISyncPairSettingsStore _syncPairs;
        private readonly ISyncSupervisor _supervisor;
        private readonly ILocalSyncRootWatcherFactory _watcherFactory;
        private readonly Dictionary<Guid, PendingLocalSyncRequest> _pendingSyncs = [];
        private readonly HashSet<PendingLocalSyncRequest> _pendingRequests = [];
        private readonly Dictionary<Guid, ILocalSyncRootWatcher> _watchers = [];
        private readonly Dictionary<Guid, string> _localRootPaths = [];
        private CancellationTokenSource? _lifetime;

        internal int PendingRequestCount
        {
            get
            {
                lock (_pendingGate)
                {
                    return _pendingRequests.Count;
                }
            }
        }

        internal int PendingChangedPathCount
        {
            get
            {
                lock (_pendingGate)
                {
                    return _pendingRequests.Sum(static request => request.ChangedPaths.Count);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalChangeSyncCoordinator" /> class.
        /// </summary>
        public LocalChangeSyncCoordinator(
            ISyncPairSettingsStore syncPairs,
            ISyncSupervisor supervisor,
            ILocalSyncRootWatcherFactory watcherFactory,
            TimeSpan? debounceInterval = null,
            ILogger<LocalChangeSyncCoordinator>? logger = null)
        {
            _syncPairs = syncPairs ?? throw new ArgumentNullException(nameof(syncPairs));
            _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
            _watcherFactory = watcherFactory ?? throw new ArgumentNullException(nameof(watcherFactory));
            _debounceInterval = debounceInterval ?? DefaultDebounceInterval;
            if (_debounceInterval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(debounceInterval), "Debounce interval cannot be negative.");
            }

            _logger = logger ?? NullLogger<LocalChangeSyncCoordinator>.Instance;
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    _lifetime = new CancellationTokenSource();
                    await _syncPairs.InitializeAsync(cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<SyncPairSettings> syncPairs = await _syncPairs.ListAsync(cancellationToken).ConfigureAwait(false);
                    foreach (SyncPairSettings syncPair in syncPairs.Where(static pair => pair.IsEnabled))
                    {
                        ILocalSyncRootWatcher watcher = _watcherFactory.Create(syncPair);
                        watcher.Changed += OnLocalChange;
                        _watchers[syncPair.Id] = watcher;
                        _localRootPaths[syncPair.Id] = syncPair.LocalRootPath;
                        await watcher.StartAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        private async Task StopCoreAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource? lifetime = _lifetime;
            _lifetime = null;
            lifetime?.Cancel();
            lifetime?.Dispose();

            List<PendingLocalSyncRequest> pendingSyncs;
            lock (_pendingGate)
            {
                pendingSyncs = _pendingRequests.ToList();
                foreach (PendingLocalSyncRequest pendingSync in pendingSyncs)
                {
                    pendingSync.Cancellation.Cancel();
                }

                _pendingSyncs.Clear();
                _pendingRequests.Clear();
            }

            await WaitForPendingSyncsAsync(pendingSyncs, cancellationToken).ConfigureAwait(false);

            foreach (ILocalSyncRootWatcher watcher in _watchers.Values)
            {
                watcher.Changed -= OnLocalChange;
                await watcher.StopAsync(cancellationToken).ConfigureAwait(false);
                await watcher.DisposeAsync().ConfigureAwait(false);
            }

            _watchers.Clear();
            _localRootPaths.Clear();
        }

        private void OnLocalChange(object? sender, LocalSyncRootChange change)
        {
            CancellationTokenSource? lifetime = _lifetime;
            if (lifetime is null || lifetime.IsCancellationRequested)
            {
                return;
            }

            lock (_pendingGate)
            {
                if (_pendingSyncs.TryGetValue(change.SyncPairId, out PendingLocalSyncRequest? pendingSync))
                {
                    pendingSync.RecordChange(change.FullPath, RequiresFullSync(change.Kind));
                    return;
                }

                var next = new PendingLocalSyncRequest(
                    CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token),
                    change.FullPath);
                next.RecordChange(change.FullPath, RequiresFullSync(change.Kind));
                _pendingSyncs.Add(change.SyncPairId, next);
                _pendingRequests.Add(next);
                next.Runner = RunDebouncedSyncAsync(change.SyncPairId, next);
            }
        }

        private async Task RunDebouncedSyncAsync(Guid syncPairId, PendingLocalSyncRequest request)
        {
            try
            {
                string changedPath;
                while (true)
                {
                    int observedChangeVersion = GetChangeVersion(request);
                    await Task.Delay(_debounceInterval, request.Cancellation.Token).ConfigureAwait(false);
                    if (TryGetQuietChangedPath(syncPairId, request, observedChangeVersion, out changedPath))
                    {
                        break;
                    }
                }

                RemoveCurrentPendingSync(syncPairId, request);
                _logger.LogDebug(
                    "Requesting local-change sync for {SyncPairId} after change at {ChangedPath}.",
                    syncPairId,
                    changedPath);
                SyncRunRequest syncRequest = CreateSyncRunRequest(syncPairId, request);
                await _supervisor.SyncNowAsync(syncPairId, syncRequest, request.Cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to request local-change sync for {SyncPairId}.",
                    syncPairId);
            }
            finally
            {
                CompletePendingSync(syncPairId, request);
                request.Cancellation.Dispose();
            }
        }

        private int GetChangeVersion(PendingLocalSyncRequest request)
        {
            lock (_pendingGate)
            {
                return request.ChangeVersion;
            }
        }

        private bool TryGetQuietChangedPath(
            Guid syncPairId,
            PendingLocalSyncRequest request,
            int observedChangeVersion,
            out string changedPath)
        {
            lock (_pendingGate)
            {
                changedPath = request.ChangedPath;
                return _pendingSyncs.TryGetValue(syncPairId, out PendingLocalSyncRequest? current)
                    && ReferenceEquals(current, request)
                    && request.ChangeVersion == observedChangeVersion;
            }
        }

        private void RemoveCurrentPendingSync(Guid syncPairId, PendingLocalSyncRequest request)
        {
            lock (_pendingGate)
            {
                if (_pendingSyncs.TryGetValue(syncPairId, out PendingLocalSyncRequest? current)
                    && ReferenceEquals(current, request))
                {
                    _pendingSyncs.Remove(syncPairId);
                }
            }
        }

        private void CompletePendingSync(Guid syncPairId, PendingLocalSyncRequest request)
        {
            lock (_pendingGate)
            {
                if (_pendingSyncs.TryGetValue(syncPairId, out PendingLocalSyncRequest? current)
                    && ReferenceEquals(current, request))
                {
                    _pendingSyncs.Remove(syncPairId);
                }

                _pendingRequests.Remove(request);
            }
        }

        private static async Task WaitForPendingSyncsAsync(
            IReadOnlyList<PendingLocalSyncRequest> pendingSyncs,
            CancellationToken cancellationToken)
        {
            Task[] runners = pendingSyncs
                .Select(static request => request.Runner)
                .OfType<Task>()
                .ToArray();
            if (runners.Length == 0)
            {
                return;
            }

            await Task.WhenAll(runners).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private SyncRunRequest CreateSyncRunRequest(Guid syncPairId, PendingLocalSyncRequest request)
        {
            if (request.RequiresFullSync || !_localRootPaths.TryGetValue(syncPairId, out string? localRootPath))
            {
                return SyncRunRequest.Full;
            }

            List<string> relativePaths = [];
            foreach (string changedPath in request.ChangedPaths)
            {
                if (TryGetRelativePath(localRootPath, changedPath, out string relativePath))
                {
                    relativePaths.Add(relativePath);
                }
            }

            return SyncRunRequest.ForLocalChangedPaths(relativePaths);
        }

        private static bool RequiresFullSync(LocalSyncRootChangeKind kind)
        {
            return kind is LocalSyncRootChangeKind.Renamed
                or LocalSyncRootChangeKind.Error;
        }

        private static bool TryGetRelativePath(string localRootPath, string fullPath, out string relativePath)
        {
            try
            {
                string fullRoot = Path.GetFullPath(localRootPath);
                string fullChangedPath = Path.GetFullPath(fullPath);
                string rootWithSeparator = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!fullChangedPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                    && !fullChangedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = string.Empty;
                    return false;
                }

                relativePath = Path.GetRelativePath(fullRoot, fullChangedPath).Replace('\\', '/');
                return !string.IsNullOrWhiteSpace(relativePath) && relativePath != ".";
            }
            catch (ArgumentException)
            {
                relativePath = string.Empty;
                return false;
            }
            catch (NotSupportedException)
            {
                relativePath = string.Empty;
                return false;
            }
        }
    }
}
