// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

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
    private readonly object _statusGate = new();
    private readonly ILogger<SyncPairRunner> _logger;
    private readonly SyncPairSettings _syncPair;
    private readonly ISyncPairWork _work;
    private SyncPairStatus _status;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPairRunner" /> class.
    /// </summary>
    public SyncPairRunner(
        SyncPairSettings syncPair,
        ISyncPairWork work,
        ILogger<SyncPairRunner>? logger = null)
    {
        _syncPair = syncPair ?? throw new ArgumentNullException(nameof(syncPair));
        _work = work ?? throw new ArgumentNullException(nameof(work));
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
                await _work.RunOnceAsync(_syncPair, cancellationToken).ConfigureAwait(false);
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
                SetState(SyncPairRunState.Error, exception.Message);
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
}
