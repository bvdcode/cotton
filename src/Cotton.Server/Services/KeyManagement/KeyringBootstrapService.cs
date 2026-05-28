// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text.Json;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Opens an existing keyring or creates the initial v2 keyring from legacy settings.
/// </summary>
internal sealed class KeyringBootstrapService(KeyringJournaledObjectStore _store)
{
    public const string DefaultLegacyMasterSlotId = "master:default";

    public async Task<KeyringBootstrapResult> OpenOrCreateFromV1Async(
        CottonEncryptionSettings legacySettings,
        string unlockSecret,
        Guid? instanceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(legacySettings);
        ArgumentException.ThrowIfNullOrWhiteSpace(unlockSecret);

        KeyringBootstrapResult? existing = await TryOpenLatestAsync(
            unlockSecret,
            instanceId,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await CreateInitialFromV1Async(
            legacySettings,
            unlockSecret,
            instanceId,
            cancellationToken);
    }

    public async Task<KeyringBootstrapResult?> TryOpenLatestAsync(
        string unlockSecret,
        Guid? instanceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unlockSecret);

        IReadOnlyList<KeyringLoadedObject> accessObjects = await _store.FindValidObjectsAsync(
            KeyringObjectKind.AccessEnvelope,
            cancellationToken);
        IReadOnlyList<KeyringLoadedObject> stateObjects = await _store.FindValidObjectsAsync(
            KeyringObjectKind.StateSnapshot,
            cancellationToken);

        if (accessObjects.Count == 0 && stateObjects.Count == 0)
        {
            return await _store.HasAnyKeyringEvidenceAsync(cancellationToken)
                ? throw new InvalidDataException("Keyring objects exist, but no valid keyring heads could be opened.")
                : null;
        }

        if (accessObjects.Count == 0 || stateObjects.Count == 0)
        {
            throw new InvalidDataException("Incomplete keyring replicas: both access envelope and state snapshot are required.");
        }

        int latestAccessGeneration = accessObjects.Max(x => x.Pointer.Generation);
        bool unlockedAnyAccessEnvelope = false;
        Exception? lastStateFailure = null;
        foreach (KeyringLoadedObject accessObject in accessObjects
            .Where(x => x.Pointer.Generation == latestAccessGeneration))
        {
            KeyringAccessEnvelope access;
            try
            {
                access = KeyringJson.Deserialize<KeyringAccessEnvelope>(accessObject.Bytes);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                continue;
            }

            KeyringUnwrapResult unwrap;
            try
            {
                unwrap = KeyringCryptography.TryUnwrapRootKey(access, unlockSecret);
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException)
            {
                continue;
            }

            if (!unwrap.Success || unwrap.KeyringRootKey is null)
            {
                continue;
            }

            unlockedAnyAccessEnvelope = true;
            try
            {
                foreach (KeyringLoadedObject stateObject in stateObjects)
                {
                    try
                    {
                        KeyringStateSnapshot snapshot = KeyringJson.Deserialize<KeyringStateSnapshot>(stateObject.Bytes);
                        KeyringPlainState state = KeyringCryptography.UnprotectState(
                            snapshot,
                            unwrap.KeyringRootKey);
                        ValidateOpenedKeyring(instanceId, access, state);
                        return new KeyringBootstrapResult(
                            Created: false,
                            State: state,
                            AccessEnvelope: access,
                            StatePointer: stateObject.Pointer,
                            AccessPointer: accessObject.Pointer,
                            UnlockedSlotId: unwrap.SlotId);
                    }
                    catch (Exception ex) when (ex is CryptographicException
                        or FormatException
                        or InvalidDataException
                        or JsonException)
                    {
                        lastStateFailure = ex;
                    }
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(unwrap.KeyringRootKey);
            }
        }

        if (!unlockedAnyAccessEnvelope)
        {
            throw new UnauthorizedAccessException("Keyring could not be unlocked with the supplied master key.");
        }

        throw new InvalidDataException(
            "No decryptable keyring state snapshot matched an unlocked access envelope.",
            lastStateFailure);
    }

    private async Task<KeyringBootstrapResult> CreateInitialFromV1Async(
        CottonEncryptionSettings legacySettings,
        string unlockSecret,
        Guid? instanceId,
        CancellationToken cancellationToken)
    {
        Guid resolvedInstanceId = instanceId ?? Guid.NewGuid();
        byte[] rootKey = KeyringCryptography.GenerateKeyMaterial();
        try
        {
            KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
                legacySettings,
                resolvedInstanceId);
            KeyringStateSnapshot snapshot = KeyringCryptography.ProtectState(state, rootKey);
            byte[] snapshotBytes = KeyringJson.SerializeToUtf8Bytes(snapshot);
            KeyringObjectPointer statePointer = await _store.CommitAsync(
                KeyringObjectKind.StateSnapshot,
                state.StateGeneration,
                snapshotBytes,
                cancellationToken);

            KeyringAccessEnvelope access = KeyringCryptography.CreateLegacyMasterAccessEnvelope(
                resolvedInstanceId,
                state.KeyringId,
                state.RootEpoch,
                generation: 1,
                parentHash: null,
                rootKey,
                unlockSecret,
                DefaultLegacyMasterSlotId,
                DateTimeOffset.UtcNow);
            byte[] accessBytes = KeyringJson.SerializeToUtf8Bytes(access);
            KeyringObjectPointer accessPointer = await _store.CommitAsync(
                KeyringObjectKind.AccessEnvelope,
                access.Generation,
                accessBytes,
                cancellationToken);

            return new KeyringBootstrapResult(
                Created: true,
                State: state,
                AccessEnvelope: access,
                StatePointer: statePointer,
                AccessPointer: accessPointer,
                UnlockedSlotId: DefaultLegacyMasterSlotId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKey);
        }
    }

    private static void ValidateOpenedKeyring(
        Guid? expectedInstanceId,
        KeyringAccessEnvelope access,
        KeyringPlainState state)
    {
        if ((expectedInstanceId.HasValue
                && (access.InstanceId != expectedInstanceId.Value || state.InstanceId != expectedInstanceId.Value))
            || access.KeyringId != state.KeyringId
            || access.RootEpoch != state.RootEpoch)
        {
            throw new InvalidDataException("Opened keyring does not match this Cotton instance.");
        }
    }
}

internal sealed record KeyringBootstrapResult(
    bool Created,
    KeyringPlainState State,
    KeyringAccessEnvelope AccessEnvelope,
    KeyringObjectPointer StatePointer,
    KeyringObjectPointer AccessPointer,
    string? UnlockedSlotId);
