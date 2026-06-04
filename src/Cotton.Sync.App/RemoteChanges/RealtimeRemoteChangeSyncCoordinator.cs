// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sdk.Realtime;
using Cotton.Sync.App.Supervision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.RemoteChanges;

/// <summary>
/// Listens to remote realtime events and requests debounced sync passes.
/// </summary>
public sealed class RealtimeRemoteChangeSyncCoordinator : IRemoteChangeSyncCoordinator
{
    private static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(750);

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _pendingGate = new();
    private readonly TimeSpan _debounceInterval;
    private readonly ILogger<RealtimeRemoteChangeSyncCoordinator> _logger;
    private readonly ICottonRealtimeClient _realtime;
    private readonly ISessionRevocationHandler _sessionRevocationHandler;
    private readonly ISyncSupervisor _supervisor;
    private CancellationTokenSource? _lifetime;
    private PendingRemoteSyncRequest? _pendingSync;
    private readonly HashSet<PendingRemoteSyncRequest> _pendingRequests = [];
    private int _sessionRevocationRequested;
    private bool _isSubscribed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeRemoteChangeSyncCoordinator" /> class.
    /// </summary>
    public RealtimeRemoteChangeSyncCoordinator(
        ICottonRealtimeClient realtime,
        ISyncSupervisor supervisor,
        TimeSpan? debounceInterval = null,
        ISessionRevocationHandler? sessionRevocationHandler = null,
        ILogger<RealtimeRemoteChangeSyncCoordinator>? logger = null)
    {
        _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _sessionRevocationHandler = sessionRevocationHandler ?? NullSessionRevocationHandler.Instance;
        _debounceInterval = debounceInterval ?? DefaultDebounceInterval;
        if (_debounceInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceInterval), "Debounce interval cannot be negative.");
        }

        _logger = logger ?? NullLogger<RealtimeRemoteChangeSyncCoordinator>.Instance;
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
                Interlocked.Exchange(ref _sessionRevocationRequested, 0);
                _lifetime = new CancellationTokenSource();
                _realtime.RemoteFileTreeChanged += OnRemoteFileTreeChanged;
                _realtime.SessionRevoked += OnSessionRevoked;
                _isSubscribed = true;
                await _realtime.StartAsync(cancellationToken).ConfigureAwait(false);
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

        List<PendingRemoteSyncRequest> pendingSyncs;
        lock (_pendingGate)
        {
            pendingSyncs = _pendingRequests.ToList();
            _pendingSync = null;
            _pendingRequests.Clear();
        }

        foreach (PendingRemoteSyncRequest pendingSync in pendingSyncs)
        {
            pendingSync.Cancellation.Cancel();
        }

        await WaitForPendingSyncsAsync(pendingSyncs, cancellationToken).ConfigureAwait(false);

        if (_isSubscribed)
        {
            _realtime.RemoteFileTreeChanged -= OnRemoteFileTreeChanged;
            _realtime.SessionRevoked -= OnSessionRevoked;
            _isSubscribed = false;
            await _realtime.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnRemoteFileTreeChanged(object? sender, CottonRealtimeEvent change)
    {
        CancellationTokenSource? lifetime = _lifetime;
        if (lifetime is null || lifetime.IsCancellationRequested)
        {
            return;
        }

        var next = new PendingRemoteSyncRequest(CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token));
        PendingRemoteSyncRequest? previous;
        lock (_pendingGate)
        {
            previous = _pendingSync;
            _pendingSync = next;
            _pendingRequests.Add(next);
            next.Runner = RunDebouncedSyncAsync(change.MethodName, next);
        }

        previous?.Cancellation.Cancel();
    }

    private void OnSessionRevoked(object? sender, CottonRealtimeEvent change)
    {
        CancellationTokenSource? lifetime = _lifetime;
        if (lifetime is null || lifetime.IsCancellationRequested)
        {
            return;
        }

        if (Interlocked.Exchange(ref _sessionRevocationRequested, 1) == 1)
        {
            return;
        }

        CancelPendingSyncRequests();
        _ = HandleSessionRevokedAsync(change.MethodName, lifetime.Token);
    }

    private async Task HandleSessionRevokedAsync(string methodName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogWarning(
                "Handling server session revocation after realtime event {MethodName}.",
                methodName);
            await _sessionRevocationHandler.HandleSessionRevokedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to handle server session revocation after realtime event {MethodName}.",
                methodName);
        }
        finally
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task RunDebouncedSyncAsync(string methodName, PendingRemoteSyncRequest request)
    {
        try
        {
            await Task.Delay(_debounceInterval, request.Cancellation.Token).ConfigureAwait(false);
            RemoveCurrentPendingSync(request);
            _logger.LogDebug(
                "Requesting remote-change sync after realtime event {MethodName}.",
                methodName);
            await _supervisor.SyncAllAsync(request.Cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to request remote-change sync after realtime event {MethodName}.",
                methodName);
        }
        finally
        {
            CompletePendingSync(request);
            request.Cancellation.Dispose();
        }
    }

    private void RemoveCurrentPendingSync(PendingRemoteSyncRequest request)
    {
        lock (_pendingGate)
        {
            if (ReferenceEquals(_pendingSync, request))
            {
                _pendingSync = null;
            }
        }
    }

    private void CompletePendingSync(PendingRemoteSyncRequest request)
    {
        lock (_pendingGate)
        {
            if (ReferenceEquals(_pendingSync, request))
            {
                _pendingSync = null;
            }

            _pendingRequests.Remove(request);
        }
    }

    private void CancelPendingSyncRequests()
    {
        List<PendingRemoteSyncRequest> pendingSyncs;
        lock (_pendingGate)
        {
            _pendingSync = null;
            pendingSyncs = _pendingRequests.ToList();
        }

        foreach (PendingRemoteSyncRequest pendingSync in pendingSyncs)
        {
            pendingSync.Cancellation.Cancel();
        }
    }

    private static async Task WaitForPendingSyncsAsync(
        IReadOnlyList<PendingRemoteSyncRequest> pendingSyncs,
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

    private sealed class PendingRemoteSyncRequest
    {
        public PendingRemoteSyncRequest(CancellationTokenSource cancellation)
        {
            Cancellation = cancellation;
        }

        public CancellationTokenSource Cancellation { get; }

        public Task? Runner { get; set; }
    }
}
