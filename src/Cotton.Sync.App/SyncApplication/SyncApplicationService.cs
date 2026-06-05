// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Continuous;
using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.RemoteChanges;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncPairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.SyncApplication;

/// <summary>
/// Provides high-level sync-client commands over validated application state.
/// </summary>
public sealed class SyncApplicationService : ISyncApplicationService
{
    private readonly SemaphoreSlim _syncCoreGate = new(1, 1);
    private readonly IAuthFlow _authFlow;
    private readonly ILocalChangeSyncCoordinator _localChanges;
    private readonly IPeriodicSyncCoordinator _periodicSync;
    private readonly IPlatformCommandService _platformCommands;
    private readonly IAppPreferencesStore _preferences;
    private readonly ISyncPairPrerequisiteValidator _prerequisites;
    private readonly IRemoteChangeSyncCoordinator _remoteChanges;
    private readonly ISyncSupervisor _supervisor;
    private readonly ISyncPairSettingsStore _syncPairs;
    private readonly SyncPairSettingsValidator _validator;
    private readonly ILogger<SyncApplicationService> _logger;
    private bool _isSyncCoreStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncApplicationService" /> class.
    /// </summary>
    public SyncApplicationService(
        ISyncPairSettingsStore syncPairs,
        ISyncPairPrerequisiteValidator prerequisites,
        IAppPreferencesStore preferences,
        IAuthFlow authFlow,
        ISyncSupervisor supervisor,
        IPlatformCommandService platformCommands,
        ILocalChangeSyncCoordinator? localChanges = null,
        IRemoteChangeSyncCoordinator? remoteChanges = null,
        IPeriodicSyncCoordinator? periodicSync = null,
        SyncPairSettingsValidator? validator = null,
        ILogger<SyncApplicationService>? logger = null)
    {
        _syncPairs = syncPairs ?? throw new ArgumentNullException(nameof(syncPairs));
        _prerequisites = prerequisites ?? throw new ArgumentNullException(nameof(prerequisites));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _authFlow = authFlow ?? throw new ArgumentNullException(nameof(authFlow));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _platformCommands = platformCommands ?? throw new ArgumentNullException(nameof(platformCommands));
        _localChanges = localChanges ?? NullLocalChangeSyncCoordinator.Instance;
        _remoteChanges = remoteChanges ?? NullRemoteChangeSyncCoordinator.Instance;
        _periodicSync = periodicSync ?? NullPeriodicSyncCoordinator.Instance;
        _validator = validator ?? new SyncPairSettingsValidator();
        _logger = logger ?? NullLogger<SyncApplicationService>.Instance;
    }

    /// <inheritdoc />
    public Task<AuthSession> SignInAsync(
        PasswordSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        return _authFlow.SignInAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthSession> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        AuthSession session = await _authFlow.RestoreSessionAsync(cancellationToken).ConfigureAwait(false);
        await StartSyncCoreAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _remoteChanges.StopAsync(cancellationToken).ConfigureAwait(false);
        await _periodicSync.StopAsync(cancellationToken).ConfigureAwait(false);
        await _localChanges.StopAsync(cancellationToken).ConfigureAwait(false);
        await _authFlow.SignOutAsync(cancellationToken).ConfigureAwait(false);
        await _supervisor.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AppPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        await _preferences.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return await _preferences.GetAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SavePreferencesAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await _preferences.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _preferences.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncPairSettings>> ListSyncPairsAsync(CancellationToken cancellationToken = default)
    {
        await _syncPairs.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return await _syncPairs.ListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SyncPairSettings?> GetSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        await _syncPairs.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return await _syncPairs.GetAsync(syncPairId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SyncPairSaveResult> SaveSyncPairAsync(
        SyncPairSettings syncPair,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncPair);
        await _syncPairs.InitializeAsync(cancellationToken).ConfigureAwait(false);
        List<SyncPairSettings> current = (await _syncPairs.ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
        int existingIndex = current.FindIndex(item => item.Id == syncPair.Id);
        if (existingIndex >= 0)
        {
            current[existingIndex] = syncPair;
        }
        else
        {
            current.Add(syncPair);
        }

        SyncPairValidationResult validation = _validator.Validate(current);
        if (!validation.IsValid)
        {
            return SyncPairSaveResult.Rejected(validation);
        }

        IReadOnlyList<SyncPairValidationError> prerequisiteErrors = await _prerequisites
            .ValidateAsync(syncPair, cancellationToken)
            .ConfigureAwait(false);
        if (prerequisiteErrors.Count > 0)
        {
            return SyncPairSaveResult.Rejected(new SyncPairValidationResult(prerequisiteErrors));
        }

        await _syncPairs.UpsertAsync(syncPair, cancellationToken).ConfigureAwait(false);
        await RestartSyncCoreIfStartedAsync(cancellationToken).ConfigureAwait(false);
        return SyncPairSaveResult.Saved(validation);
    }

    /// <inheritdoc />
    public async Task DeleteSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        await _syncPairs.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _syncPairs.DeleteAsync(syncPairId, cancellationToken).ConfigureAwait(false);
        await RestartSyncCoreIfStartedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StartSyncAsync(CancellationToken cancellationToken = default)
    {
        return StartSyncCoreAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        return _supervisor.SyncAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        return _supervisor.SyncNowAsync(syncPairId, cancellationToken);
    }

    /// <inheritdoc />
    public Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        return _supervisor.PauseAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task PauseAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        return _supervisor.PauseAsync(syncPairId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        return _supervisor.ResumeAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        return _supervisor.ResumeAsync(syncPairId, cancellationToken);
    }

    /// <inheritdoc />
    public Task StopSyncAsync(CancellationToken cancellationToken = default)
    {
        return StopSyncCoreAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
    {
        return _platformCommands.OpenFolderAsync(localPath, cancellationToken);
    }

    /// <inheritdoc />
    public Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
    {
        return _platformCommands.OpenWebAsync(url, cancellationToken);
    }

    private async Task StartSyncCoreAsync(CancellationToken cancellationToken)
    {
        await _syncCoreGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StartSyncCoreUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _syncCoreGate.Release();
        }
    }

    private async Task StopSyncCoreAsync(CancellationToken cancellationToken)
    {
        await _syncCoreGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopSyncCoreUnlockedAsync(cancellationToken, force: true).ConfigureAwait(false);
        }
        finally
        {
            _syncCoreGate.Release();
        }
    }

    private async Task RestartSyncCoreIfStartedAsync(CancellationToken cancellationToken)
    {
        await _syncCoreGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isSyncCoreStarted)
            {
                return;
            }

            await StopSyncCoreUnlockedAsync(cancellationToken, force: false).ConfigureAwait(false);
            await StartSyncCoreUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _syncCoreGate.Release();
        }
    }

    private async Task StartSyncCoreUnlockedAsync(CancellationToken cancellationToken)
    {
        if (_isSyncCoreStarted)
        {
            await StopSyncCoreUnlockedAsync(cancellationToken, force: false).ConfigureAwait(false);
        }

        var startedComponents = new List<StartedSyncComponent>();

        try
        {
            await _supervisor.StartAsync(cancellationToken).ConfigureAwait(false);
            startedComponents.Add(new StartedSyncComponent(
                "sync supervisor",
                token => _supervisor.StopAsync(token)));

            await _localChanges.StartAsync(cancellationToken).ConfigureAwait(false);
            startedComponents.Add(new StartedSyncComponent(
                "local change coordinator",
                token => _localChanges.StopAsync(token)));

            await _remoteChanges.StartAsync(cancellationToken).ConfigureAwait(false);
            startedComponents.Add(new StartedSyncComponent(
                "remote change coordinator",
                token => _remoteChanges.StopAsync(token)));

            await _periodicSync.StartAsync(cancellationToken).ConfigureAwait(false);
            startedComponents.Add(new StartedSyncComponent(
                "periodic sync coordinator",
                token => _periodicSync.StopAsync(token)));
            _isSyncCoreStarted = true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to start sync background components.");
            await RollBackStartedComponentsAsync(startedComponents).ConfigureAwait(false);
            _isSyncCoreStarted = false;
            throw;
        }
    }

    private async Task StopSyncCoreUnlockedAsync(CancellationToken cancellationToken, bool force)
    {
        if (!_isSyncCoreStarted && !force)
        {
            return;
        }

        await _remoteChanges.StopAsync(cancellationToken).ConfigureAwait(false);
        await _periodicSync.StopAsync(cancellationToken).ConfigureAwait(false);
        await _localChanges.StopAsync(cancellationToken).ConfigureAwait(false);
        await _supervisor.StopAsync(cancellationToken).ConfigureAwait(false);
        _isSyncCoreStarted = false;
    }

    private async Task RollBackStartedComponentsAsync(IReadOnlyList<StartedSyncComponent> startedComponents)
    {
        for (int index = startedComponents.Count - 1; index >= 0; index--)
        {
            StartedSyncComponent component = startedComponents[index];

            try
            {
                await component.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to stop {ComponentName} during sync startup rollback.",
                    component.Name);
            }
        }
    }

    private sealed record StartedSyncComponent(
        string Name,
        Func<CancellationToken, Task> StopAsync);
}
