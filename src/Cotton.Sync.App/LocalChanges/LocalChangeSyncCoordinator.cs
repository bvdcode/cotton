// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncPairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.LocalChanges;

/// <summary>
/// Watches local sync roots and requests debounced sync passes.
/// </summary>
public sealed class LocalChangeSyncCoordinator : ILocalChangeSyncCoordinator
{
    private static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(750);

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _pendingGate = new();
    private readonly TimeSpan _debounceInterval;
    private readonly ILogger<LocalChangeSyncCoordinator> _logger;
    private readonly ISyncPairSettingsStore _syncPairs;
    private readonly ISyncSupervisor _supervisor;
    private readonly ILocalSyncRootWatcherFactory _watcherFactory;
    private readonly Dictionary<Guid, CancellationTokenSource> _pendingSyncs = [];
    private readonly Dictionary<Guid, ILocalSyncRootWatcher> _watchers = [];
    private CancellationTokenSource? _lifetime;

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

        List<CancellationTokenSource> pendingSyncs;
        lock (_pendingGate)
        {
            pendingSyncs = _pendingSyncs.Values.ToList();
            _pendingSyncs.Clear();
        }

        foreach (CancellationTokenSource pendingSync in pendingSyncs)
        {
            pendingSync.Cancel();
            pendingSync.Dispose();
        }

        foreach (ILocalSyncRootWatcher watcher in _watchers.Values)
        {
            watcher.Changed -= OnLocalChange;
            await watcher.StopAsync(cancellationToken).ConfigureAwait(false);
            await watcher.DisposeAsync().ConfigureAwait(false);
        }

        _watchers.Clear();
    }

    private void OnLocalChange(object? sender, LocalSyncRootChange change)
    {
        CancellationTokenSource? lifetime = _lifetime;
        if (lifetime is null || lifetime.IsCancellationRequested)
        {
            return;
        }

        CancellationTokenSource next = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        CancellationTokenSource? previous;
        lock (_pendingGate)
        {
            _pendingSyncs.TryGetValue(change.SyncPairId, out previous);
            _pendingSyncs[change.SyncPairId] = next;
        }

        previous?.Cancel();
        previous?.Dispose();
        _ = RunDebouncedSyncAsync(change.SyncPairId, change.FullPath, next);
    }

    private async Task RunDebouncedSyncAsync(Guid syncPairId, string changedPath, CancellationTokenSource request)
    {
        try
        {
            await Task.Delay(_debounceInterval, request.Token).ConfigureAwait(false);
            RemovePendingSync(syncPairId, request);
            _logger.LogDebug(
                "Requesting local-change sync for {SyncPairId} after change at {ChangedPath}.",
                syncPairId,
                changedPath);
            await _supervisor.SyncNowAsync(syncPairId, request.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
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
            RemovePendingSync(syncPairId, request);
            request.Dispose();
        }
    }

    private void RemovePendingSync(Guid syncPairId, CancellationTokenSource request)
    {
        lock (_pendingGate)
        {
            if (_pendingSyncs.TryGetValue(syncPairId, out CancellationTokenSource? current)
                && ReferenceEquals(current, request))
            {
                _pendingSyncs.Remove(syncPairId);
            }
        }
    }
}
