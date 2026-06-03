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
    private CancellationTokenSource? _pendingSync;
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
            Interlocked.Exchange(ref _sessionRevocationRequested, 0);
            _lifetime = new CancellationTokenSource();
            _realtime.RemoteFileTreeChanged += OnRemoteFileTreeChanged;
            _realtime.SessionRevoked += OnSessionRevoked;
            _isSubscribed = true;
            await _realtime.StartAsync(cancellationToken).ConfigureAwait(false);
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

        CancellationTokenSource? pendingSync;
        lock (_pendingGate)
        {
            pendingSync = _pendingSync;
            _pendingSync = null;
        }

        pendingSync?.Cancel();
        pendingSync?.Dispose();

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

        CancellationTokenSource next = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        CancellationTokenSource? previous;
        lock (_pendingGate)
        {
            previous = _pendingSync;
            _pendingSync = next;
        }

        previous?.Cancel();
        previous?.Dispose();
        _ = RunDebouncedSyncAsync(change.MethodName, next);
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

        CancelPendingSyncRequest();
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

    private async Task RunDebouncedSyncAsync(string methodName, CancellationTokenSource request)
    {
        try
        {
            await Task.Delay(_debounceInterval, request.Token).ConfigureAwait(false);
            RemovePendingSync(request);
            _logger.LogDebug(
                "Requesting remote-change sync after realtime event {MethodName}.",
                methodName);
            await _supervisor.SyncAllAsync(request.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
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
            RemovePendingSync(request);
            request.Dispose();
        }
    }

    private void RemovePendingSync(CancellationTokenSource request)
    {
        lock (_pendingGate)
        {
            if (ReferenceEquals(_pendingSync, request))
            {
                _pendingSync = null;
            }
        }
    }

    private void CancelPendingSyncRequest()
    {
        CancellationTokenSource? pendingSync;
        lock (_pendingGate)
        {
            pendingSync = _pendingSync;
            _pendingSync = null;
        }

        pendingSync?.Cancel();
        pendingSync?.Dispose();
    }
}
