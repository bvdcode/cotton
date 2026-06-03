// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.SyncApplication;

/// <summary>
/// Provides high-level sync-client commands over validated application state.
/// </summary>
public sealed class SyncApplicationService : ISyncApplicationService
{
    private readonly ISyncPairPrerequisiteValidator _prerequisites;
    private readonly ISyncPairSettingsStore _syncPairs;
    private readonly SyncPairSettingsValidator _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncApplicationService" /> class.
    /// </summary>
    public SyncApplicationService(
        ISyncPairSettingsStore syncPairs,
        ISyncPairPrerequisiteValidator prerequisites,
        SyncPairSettingsValidator? validator = null)
    {
        _syncPairs = syncPairs ?? throw new ArgumentNullException(nameof(syncPairs));
        _prerequisites = prerequisites ?? throw new ArgumentNullException(nameof(prerequisites));
        _validator = validator ?? new SyncPairSettingsValidator();
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
}
