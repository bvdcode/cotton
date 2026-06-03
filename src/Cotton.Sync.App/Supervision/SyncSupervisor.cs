// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;
using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Supervision;

/// <summary>
/// Coordinates sync pair runners and publishes aggregate application status.
/// </summary>
public sealed class SyncSupervisor : ISyncSupervisor
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, ISyncPairRunner> _runners = [];
    private readonly ISyncPairRunnerFactory _runnerFactory;
    private readonly IAppStatusPublisher _statusPublisher;
    private readonly ISyncPairSettingsStore _syncPairs;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncSupervisor" /> class.
    /// </summary>
    public SyncSupervisor(
        ISyncPairSettingsStore syncPairs,
        ISyncPairRunnerFactory runnerFactory,
        IAppStatusPublisher statusPublisher)
    {
        _syncPairs = syncPairs ?? throw new ArgumentNullException(nameof(syncPairs));
        _runnerFactory = runnerFactory ?? throw new ArgumentNullException(nameof(runnerFactory));
        _statusPublisher = statusPublisher ?? throw new ArgumentNullException(nameof(statusPublisher));
    }

    /// <inheritdoc />
    public IReadOnlyList<SyncPairStatus> CurrentStatuses => CreatePairStatusSnapshot();

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _runners.Clear();
            await _syncPairs.InitializeAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<SyncPairSettings> syncPairs = await _syncPairs.ListAsync(cancellationToken).ConfigureAwait(false);
            foreach (SyncPairSettings syncPair in syncPairs)
            {
                ISyncPairRunner runner = _runnerFactory.Create(syncPair);
                _runners[syncPair.Id] = runner;
                await runner.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    /// <inheritdoc />
    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ISyncPairRunner runner in _runners.Values)
            {
                await runner.SyncNowAsync(cancellationToken).ConfigureAwait(false);
            }

            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    /// <inheritdoc />
    public async Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await GetRunner(syncPairId).SyncNowAsync(cancellationToken).ConfigureAwait(false);
            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    /// <inheritdoc />
    public async Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ISyncPairRunner runner in _runners.Values)
            {
                await runner.PauseAsync(cancellationToken).ConfigureAwait(false);
            }

            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    /// <inheritdoc />
    public async Task PauseAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await GetRunner(syncPairId).PauseAsync(cancellationToken).ConfigureAwait(false);
            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    /// <inheritdoc />
    public async Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ISyncPairRunner runner in _runners.Values)
            {
                await runner.ResumeAsync(cancellationToken).ConfigureAwait(false);
            }

            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    /// <inheritdoc />
    public async Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await GetRunner(syncPairId).ResumeAsync(cancellationToken).ConfigureAwait(false);
            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ISyncPairRunner runner in _runners.Values)
            {
                await runner.StopAsync(cancellationToken).ConfigureAwait(false);
            }

            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    private ISyncPairRunner GetRunner(Guid syncPairId)
    {
        if (_runners.TryGetValue(syncPairId, out ISyncPairRunner? runner))
        {
            return runner;
        }

        throw new InvalidOperationException($"Sync pair runner is not started: {syncPairId}.");
    }

    private SyncAppStatus CreateAppStatusSnapshot()
    {
        return new SyncAppStatus(
            _statusPublisher.Current.IsAuthenticated,
            CreatePairStatusSnapshot(),
            DateTime.UtcNow);
    }

    private IReadOnlyList<SyncPairStatus> CreatePairStatusSnapshot()
    {
        return _runners.Values
            .Select(static runner => runner.Status)
            .OrderBy(static status => status.DisplayName, StringComparer.Ordinal)
            .ThenBy(static status => status.SyncPairId)
            .ToList();
    }
}
