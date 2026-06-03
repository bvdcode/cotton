// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.SyncApplication;

/// <summary>
/// Provides high-level sync-client commands over validated application state.
/// </summary>
public sealed class SyncApplicationService : ISyncApplicationService
{
    private readonly IAuthFlow _authFlow;
    private readonly ILocalChangeSyncCoordinator _localChanges;
    private readonly IPlatformCommandService _platformCommands;
    private readonly IAppPreferencesStore _preferences;
    private readonly ISyncPairPrerequisiteValidator _prerequisites;
    private readonly ISyncSupervisor _supervisor;
    private readonly ISyncPairSettingsStore _syncPairs;
    private readonly SyncPairSettingsValidator _validator;

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
        SyncPairSettingsValidator? validator = null)
    {
        _syncPairs = syncPairs ?? throw new ArgumentNullException(nameof(syncPairs));
        _prerequisites = prerequisites ?? throw new ArgumentNullException(nameof(prerequisites));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _authFlow = authFlow ?? throw new ArgumentNullException(nameof(authFlow));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _platformCommands = platformCommands ?? throw new ArgumentNullException(nameof(platformCommands));
        _localChanges = localChanges ?? NullLocalChangeSyncCoordinator.Instance;
        _validator = validator ?? new SyncPairSettingsValidator();
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
        await _supervisor.StartAsync(cancellationToken).ConfigureAwait(false);
        await _localChanges.StartAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
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
        return SyncPairSaveResult.Saved(validation);
    }

    /// <inheritdoc />
    public async Task DeleteSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        await _syncPairs.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _syncPairs.DeleteAsync(syncPairId, cancellationToken).ConfigureAwait(false);
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
        await _supervisor.StartAsync(cancellationToken).ConfigureAwait(false);
        await _localChanges.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StopSyncCoreAsync(CancellationToken cancellationToken)
    {
        await _localChanges.StopAsync(cancellationToken).ConfigureAwait(false);
        await _supervisor.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
