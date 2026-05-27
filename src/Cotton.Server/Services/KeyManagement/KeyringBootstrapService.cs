// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

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
        Guid instanceId,
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
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unlockSecret);

        KeyringLoadedObject? accessObject = await _store.FindLatestValidAsync(
            KeyringObjectKind.AccessEnvelope,
            cancellationToken);
        KeyringLoadedObject? stateObject = await _store.FindLatestValidAsync(
            KeyringObjectKind.StateSnapshot,
            cancellationToken);
        if (accessObject is null && stateObject is null)
        {
            return null;
        }

        if (accessObject is null || stateObject is null)
        {
            return null;
        }

        KeyringAccessEnvelope access = KeyringJson.Deserialize<KeyringAccessEnvelope>(accessObject.Bytes);
        KeyringUnwrapResult unwrap = KeyringCryptography.TryUnwrapRootKey(access, unlockSecret);
        if (!unwrap.Success || unwrap.KeyringRootKey is null)
        {
            throw new UnauthorizedAccessException("Keyring could not be unlocked with the supplied master key.");
        }

        try
        {
            KeyringStateSnapshot snapshot = KeyringJson.Deserialize<KeyringStateSnapshot>(stateObject.Bytes);
            KeyringPlainState state = KeyringCryptography.UnprotectState(snapshot, unwrap.KeyringRootKey);
            ValidateOpenedKeyring(instanceId, access, state);
            return new KeyringBootstrapResult(
                Created: false,
                State: state,
                AccessEnvelope: access,
                StatePointer: stateObject.Pointer,
                AccessPointer: accessObject.Pointer,
                UnlockedSlotId: unwrap.SlotId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrap.KeyringRootKey);
        }
    }

    private async Task<KeyringBootstrapResult> CreateInitialFromV1Async(
        CottonEncryptionSettings legacySettings,
        string unlockSecret,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        byte[] rootKey = KeyringCryptography.GenerateKeyMaterial();
        try
        {
            KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
                legacySettings,
                instanceId);
            KeyringStateSnapshot snapshot = KeyringCryptography.ProtectState(state, rootKey);
            byte[] snapshotBytes = KeyringJson.SerializeToUtf8Bytes(snapshot);
            KeyringObjectPointer statePointer = await _store.CommitAsync(
                KeyringObjectKind.StateSnapshot,
                state.StateGeneration,
                snapshotBytes,
                cancellationToken);

            KeyringAccessEnvelope access = KeyringCryptography.CreateLegacyMasterAccessEnvelope(
                instanceId,
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
        Guid expectedInstanceId,
        KeyringAccessEnvelope access,
        KeyringPlainState state)
    {
        if (access.InstanceId != expectedInstanceId
            || state.InstanceId != expectedInstanceId
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
