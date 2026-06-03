// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncPairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.Runners;

/// <summary>
/// Manages runtime state and one-shot synchronization requests for one sync pair.
/// </summary>
public sealed class SyncPairRunner : ISyncPairRunner
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _syncRequestGate = new();
    private readonly object _statusGate = new();
    private readonly ILogger<SyncPairRunner> _logger;
    private readonly SyncPairRunnerRetryOptions _retryOptions;
    private readonly SyncPairSettings _syncPair;
    private readonly ISyncPairWork _work;
    private bool _isSyncInProgress;
    private bool _pendingSyncRequested;
    private SyncPairStatus _status;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPairRunner" /> class.
    /// </summary>
    public SyncPairRunner(
        SyncPairSettings syncPair,
        ISyncPairWork work,
        SyncPairRunnerRetryOptions? retryOptions = null,
        ILogger<SyncPairRunner>? logger = null)
    {
        _syncPair = syncPair ?? throw new ArgumentNullException(nameof(syncPair));
        _work = work ?? throw new ArgumentNullException(nameof(work));
        _retryOptions = (retryOptions ?? SyncPairRunnerRetryOptions.Default).Normalize();
        _logger = logger ?? NullLogger<SyncPairRunner>.Instance;
        _status = CreateStatus(syncPair.IsEnabled ? SyncPairRunState.Idle : SyncPairRunState.Disabled);
    }

    /// <inheritdoc />
    public Guid SyncPairId => _syncPair.Id;

    /// <inheritdoc />
    public SyncPairStatus Status
    {
        get
        {
            lock (_statusGate)
            {
                return _status;
            }
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetState(_syncPair.IsEnabled ? SyncPairRunState.Idle : SyncPairRunState.Disabled);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Status.State != SyncPairRunState.Disabled)
            {
                SetState(SyncPairRunState.Paused);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetState(_syncPair.IsEnabled ? SyncPairRunState.Idle : SyncPairRunState.Disabled);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (!TryStartSyncLoop())
        {
            return;
        }

        try
        {
            bool runAgain;
            do
            {
                await RunSingleSyncAsync(cancellationToken).ConfigureAwait(false);
                runAgain = CompleteSyncPassOrTakeQueued();
            }
            while (runAgain);
        }
        catch
        {
            FinishSyncLoopAfterFailure();
            throw;
        }
    }

    private async Task RunSingleSyncAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SyncPairRunState currentState = Status.State;
            if (!_syncPair.IsEnabled || currentState is SyncPairRunState.Disabled or SyncPairRunState.Paused)
            {
                return;
            }

            SetState(SyncPairRunState.Syncing);
            try
            {
                await RunWorkWithRetryAsync(cancellationToken).ConfigureAwait(false);
                SetState(SyncPairRunState.Idle);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                SetState(SyncPairRunState.Idle);
                _logger.LogDebug(
                    exception,
                    "Sync pair runner was canceled for {SyncPairId}.",
                    _syncPair.Id);
                throw;
            }
            catch (Exception exception)
            {
                SetState(IsTransientNetworkFailure(exception) ? SyncPairRunState.Offline : SyncPairRunState.Error, exception.Message);
                _logger.LogError(
                    exception,
                    "Sync pair runner failed for {SyncPairId}.",
                    _syncPair.Id);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private bool TryStartSyncLoop()
    {
        lock (_syncRequestGate)
        {
            if (_isSyncInProgress)
            {
                _pendingSyncRequested = true;
                return false;
            }

            _isSyncInProgress = true;
            _pendingSyncRequested = false;
            return true;
        }
    }

    private bool CompleteSyncPassOrTakeQueued()
    {
        lock (_syncRequestGate)
        {
            if (_pendingSyncRequested)
            {
                _pendingSyncRequested = false;
                return true;
            }

            _isSyncInProgress = false;
            return false;
        }
    }

    private void FinishSyncLoopAfterFailure()
    {
        lock (_syncRequestGate)
        {
            _isSyncInProgress = false;
            _pendingSyncRequested = false;
        }
    }

    private async Task RunWorkWithRetryAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= _retryOptions.MaxAttempts; attempt++)
        {
            try
            {
                await _work.RunOnceAsync(_syncPair, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsTransientNetworkFailure(exception) && attempt < _retryOptions.MaxAttempts)
            {
                TimeSpan delay = GetRetryDelay(attempt);
                SetState(SyncPairRunState.Offline, exception.Message);
                _logger.LogWarning(
                    exception,
                    "Transient sync failure for {SyncPairId}; retrying attempt {NextAttempt} of {MaxAttempts} after {Delay}.",
                    _syncPair.Id,
                    attempt + 1,
                    _retryOptions.MaxAttempts,
                    delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                SetState(SyncPairRunState.Syncing);
            }
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetState(SyncPairRunState.Disabled);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void SetState(SyncPairRunState state, string? lastError = null)
    {
        lock (_statusGate)
        {
            _status = CreateStatus(state, lastError);
        }
    }

    private SyncPairStatus CreateStatus(SyncPairRunState state, string? lastError = null)
    {
        return new SyncPairStatus(
            _syncPair.Id,
            _syncPair.DisplayName,
            state,
            null,
            lastError,
            DateTime.UtcNow);
    }

    private TimeSpan GetRetryDelay(int completedAttempts)
    {
        if (_retryOptions.InitialDelay == TimeSpan.Zero || _retryOptions.MaxDelay == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        double multiplier = Math.Pow(2, Math.Max(0, completedAttempts - 1));
        double milliseconds = Math.Min(
            _retryOptions.InitialDelay.TotalMilliseconds * multiplier,
            _retryOptions.MaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static bool IsTransientNetworkFailure(Exception exception)
    {
        return exception is HttpRequestException requestException && IsTransientStatusCode(requestException.StatusCode);
    }

    private static bool IsTransientStatusCode(HttpStatusCode? statusCode)
    {
        return statusCode is null
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }
}
