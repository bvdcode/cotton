// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Coordinates administrator keyring operations that update the live runtime state.
/// </summary>
public sealed class KeyringAdminService
{
    private readonly KeyringJournaledObjectStore _store;
    private readonly KeyringRuntimeState _runtimeState;

    internal KeyringAdminService(
        KeyringJournaledObjectStore store,
        KeyringRuntimeState runtimeState)
    {
        _store = store;
        _runtimeState = runtimeState;
    }

    internal async Task<KeyringAdminRotationResult> RotateUnlockAsync(
        string currentUnlockSecret,
        string newUnlockSecret,
        CancellationToken cancellationToken = default)
    {
        if (!KeyringStartup.IsEnabled())
        {
            throw new InvalidOperationException("Keyring v2 is not enabled for this process.");
        }

        ConfigurationBuilderExtensions.ValidateRootMasterKey(currentUnlockSecret);
        ConfigurationBuilderExtensions.ValidateRootMasterKey(newUnlockSecret);
        if (string.Equals(currentUnlockSecret, newUnlockSecret, StringComparison.Ordinal))
        {
            throw new ArgumentException("New unlock secret must be different from the current unlock secret.");
        }

        var rotation = new KeyringRotationService(_store);
        KeyringRotationResult rotated = await rotation.RotateLegacyMasterUnlockAsync(
            currentUnlockSecret,
            newUnlockSecret,
            cancellationToken);
        var bootstrapResult = new KeyringBootstrapResult(
            Created: false,
            State: rotated.State,
            AccessEnvelope: rotated.AccessEnvelope,
            StatePointer: rotated.StatePointer,
            AccessPointer: rotated.AccessPointer,
            UnlockedSlotId: KeyringBootstrapService.DefaultLegacyMasterSlotId);
        _runtimeState.Set(bootstrapResult);

        return new KeyringAdminRotationResult(
            rotated.State.RootEpoch,
            rotated.AccessEnvelope.Generation,
            rotated.State.StateGeneration);
    }
}

internal sealed record KeyringAdminRotationResult(
    int RootEpoch,
    int AccessGeneration,
    int StateGeneration);
