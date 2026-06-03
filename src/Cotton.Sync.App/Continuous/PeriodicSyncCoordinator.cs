// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Supervision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.Continuous;

/// <summary>
/// Requests periodic full reconciliation as a safety fallback.
/// </summary>
public sealed class PeriodicSyncCoordinator : IPeriodicSyncCoordinator
{
    /// <summary>
    /// Default periodic safety sync interval.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly TimeSpan _interval;
    private readonly ILogger<PeriodicSyncCoordinator> _logger;
    private readonly ISyncSupervisor _supervisor;
    private CancellationTokenSource? _lifetime;
    private Task? _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicSyncCoordinator" /> class.
    /// </summary>
    public PeriodicSyncCoordinator(
        ISyncSupervisor supervisor,
        TimeSpan? interval = null,
        ILogger<PeriodicSyncCoordinator>? logger = null)
    {
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _interval = interval ?? DefaultInterval;
        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Periodic sync interval must be positive.");
        }

        _logger = logger ?? NullLogger<PeriodicSyncCoordinator>.Instance;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
            _lifetime = new CancellationTokenSource();
            _runner = RunLoopAsync(_lifetime.Token);
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
        Task? runner = _runner;
        _lifetime = null;
        _runner = null;
        if (lifetime is null)
        {
            return;
        }

        await lifetime.CancelAsync().ConfigureAwait(false);
        if (runner is null)
        {
            lifetime.Dispose();
            return;
        }

        try
        {
            await runner.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || runner.IsCanceled)
        {
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await RunSyncAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Requesting periodic safety sync.");
            await _supervisor.SyncAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Periodic safety sync failed.");
        }
    }
}
