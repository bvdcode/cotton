// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Models.Dto;
using System.Text.Json;

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

    internal async Task<KeyringRecoveryKitDto> ExportRecoveryKitAsync(
        CancellationToken cancellationToken = default)
    {
        if (!KeyringStartup.IsEnabled())
        {
            throw new InvalidOperationException("Keyring v2 is not enabled for this process.");
        }

        KeyringBootstrapResult runtime = _runtimeState.Current
            ?? throw new InvalidOperationException("Keyring recovery kit cannot be exported before runtime keyring is loaded.");
        KeyringLoadedObject? accessObject = await _store.FindLatestValidAsync(
            KeyringObjectKind.AccessEnvelope,
            cancellationToken);
        KeyringLoadedObject? stateObject = await _store.FindLatestValidAsync(
            KeyringObjectKind.StateSnapshot,
            cancellationToken);
        if (accessObject is null || stateObject is null)
        {
            throw new InvalidOperationException("Keyring recovery kit cannot be exported because access or state objects are missing.");
        }

        KeyringAccessEnvelope access = DeserializeAccessEnvelope(accessObject.Bytes);
        KeyringStateSnapshot snapshot = DeserializeStateSnapshot(stateObject.Bytes);
        if (access.InstanceId != snapshot.InstanceId
            || access.KeyringId != snapshot.KeyringId
            || access.RootEpoch != snapshot.RootEpoch)
        {
            throw new InvalidOperationException("Keyring recovery kit cannot be exported because access and state metadata do not match.");
        }

        if (runtime.State.InstanceId != snapshot.InstanceId
            || runtime.State.KeyringId != snapshot.KeyringId
            || runtime.State.RootEpoch != snapshot.RootEpoch
            || runtime.State.StateGeneration != snapshot.StateGeneration
            || runtime.AccessEnvelope.Generation != access.Generation)
        {
            throw new InvalidOperationException("Keyring recovery kit cannot be exported because the stored keyring objects do not match the loaded runtime keyring.");
        }

        return new KeyringRecoveryKitDto
        {
            ExportedAtUtc = DateTimeOffset.UtcNow,
            InstanceId = access.InstanceId,
            KeyringId = access.KeyringId,
            RootEpoch = access.RootEpoch,
            AccessGeneration = access.Generation,
            StateGeneration = snapshot.StateGeneration,
            AccessEnvelopeObjectName = accessObject.Pointer.ObjectName,
            AccessEnvelopeHash = accessObject.Pointer.Hash,
            AccessEnvelopeBase64 = Convert.ToBase64String(accessObject.Bytes),
            StateSnapshotObjectName = stateObject.Pointer.ObjectName,
            StateSnapshotHash = stateObject.Pointer.Hash,
            StateSnapshotBase64 = Convert.ToBase64String(stateObject.Bytes),
        };
    }

    private static KeyringAccessEnvelope DeserializeAccessEnvelope(byte[] bytes)
    {
        try
        {
            KeyringAccessEnvelope access = KeyringJson.Deserialize<KeyringAccessEnvelope>(bytes);
            if (access.Magic != KeyringFormat.AccessEnvelopeMagic)
            {
                throw new InvalidOperationException("Keyring access envelope has an invalid format marker.");
            }

            return access;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Keyring access envelope could not be parsed.", ex);
        }
    }

    private static KeyringStateSnapshot DeserializeStateSnapshot(byte[] bytes)
    {
        try
        {
            KeyringStateSnapshot snapshot = KeyringJson.Deserialize<KeyringStateSnapshot>(bytes);
            if (snapshot.Magic != KeyringFormat.StateSnapshotMagic)
            {
                throw new InvalidOperationException("Keyring state snapshot has an invalid format marker.");
            }

            return snapshot;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Keyring state snapshot could not be parsed.", ex);
        }
    }
}

internal sealed record KeyringAdminRotationResult(
    int RootEpoch,
    int AccessGeneration,
    int StateGeneration);
