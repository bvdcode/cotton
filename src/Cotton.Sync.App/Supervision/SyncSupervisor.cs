// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncPairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.Supervision;

/// <summary>
/// Coordinates sync pair runners and publishes aggregate application status.
/// </summary>
public sealed class SyncSupervisor : ISyncSupervisor
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, ISyncPairRunner> _runners = [];
    private readonly ILogger<SyncSupervisor> _logger;
    private readonly ISyncPairRunnerFactory _runnerFactory;
    private readonly IAppStatusPublisher _statusPublisher;
    private readonly ISyncPairSettingsStore _syncPairs;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncSupervisor" /> class.
    /// </summary>
    public SyncSupervisor(
        ISyncPairSettingsStore syncPairs,
        ISyncPairRunnerFactory runnerFactory,
        IAppStatusPublisher statusPublisher,
        ILogger<SyncSupervisor>? logger = null)
    {
        _syncPairs = syncPairs ?? throw new ArgumentNullException(nameof(syncPairs));
        _runnerFactory = runnerFactory ?? throw new ArgumentNullException(nameof(runnerFactory));
        _statusPublisher = statusPublisher ?? throw new ArgumentNullException(nameof(statusPublisher));
        _logger = logger ?? NullLogger<SyncSupervisor>.Instance;
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
            await StopRunnersAsync(cancellationToken).ConfigureAwait(false);
            _runners.Clear();
            try
            {
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
            catch
            {
                await StopRunnersAsync(CancellationToken.None).ConfigureAwait(false);
                _runners.Clear();
                throw;
            }
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
        IReadOnlyList<ISyncPairRunner> runners;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            runners = _runners.Values.ToList();
        }
        finally
        {
            _operationGate.Release();
        }

        IReadOnlyList<Exception> failures = await SyncRunnersAndCollectFailuresAsync(runners, cancellationToken)
            .ConfigureAwait(false);

        _statusPublisher.Publish(CreateAppStatusSnapshot());
        ThrowIfAnySyncFailures(failures);
    }

    /// <inheritdoc />
    public async Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        ISyncPairRunner runner;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            runner = GetRunner(syncPairId);
        }
        finally
        {
            _operationGate.Release();
        }

        await runner.SyncNowAsync(cancellationToken).ConfigureAwait(false);

        _statusPublisher.Publish(CreateAppStatusSnapshot());
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
        IReadOnlyList<ISyncPairRunner> resumedRunners;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ISyncPairRunner runner in _runners.Values)
            {
                await runner.ResumeAsync(cancellationToken).ConfigureAwait(false);
            }

            resumedRunners = _runners.Values
                .Where(static runner => runner.Status.State != SyncPairRunState.Disabled)
                .ToList();
            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
        await SyncRunnersAsync(resumedRunners, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        ISyncPairRunner? resumedRunner;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ISyncPairRunner runner = GetRunner(syncPairId);
            await runner.ResumeAsync(cancellationToken).ConfigureAwait(false);
            resumedRunner = runner.Status.State == SyncPairRunState.Disabled ? null : runner;
            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
        if (resumedRunner is not null)
        {
            await resumedRunner.SyncNowAsync(cancellationToken).ConfigureAwait(false);
            _statusPublisher.Publish(CreateAppStatusSnapshot());
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        SyncAppStatus status;
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopRunnersAsync(cancellationToken).ConfigureAwait(false);
            status = CreateAppStatusSnapshot();
        }
        finally
        {
            _operationGate.Release();
        }

        _statusPublisher.Publish(status);
    }

    private async Task StopRunnersAsync(CancellationToken cancellationToken)
    {
        foreach (ISyncPairRunner runner in _runners.Values)
        {
            await runner.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncRunnersAsync(
        IReadOnlyList<ISyncPairRunner> runners,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Exception> failures = await SyncRunnersAndCollectFailuresAsync(runners, cancellationToken)
            .ConfigureAwait(false);

        if (runners.Count > 0)
        {
            _statusPublisher.Publish(CreateAppStatusSnapshot());
        }

        ThrowIfAnySyncFailures(failures);
    }

    private async Task<IReadOnlyList<Exception>> SyncRunnersAndCollectFailuresAsync(
        IReadOnlyList<ISyncPairRunner> runners,
        CancellationToken cancellationToken)
    {
        List<Exception>? failures = null;
        foreach (ISyncPairRunner runner in runners)
        {
            try
            {
                await runner.SyncNowAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Sync runner failed for {SyncPairId}.",
                    runner.SyncPairId);
                (failures ??= []).Add(exception);
            }
        }

        return failures is null ? Array.Empty<Exception>() : failures;
    }

    private static void ThrowIfAnySyncFailures(IReadOnlyList<Exception> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("One or more sync folders failed.", failures);
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
