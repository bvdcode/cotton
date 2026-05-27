// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Performs small keyring metadata rotations without touching encrypted user chunks.
/// </summary>
internal sealed class KeyringRotationService(KeyringJournaledObjectStore _store)
{
    public async Task<KeyringRotationResult> RotateLegacyMasterUnlockAsync(
        string currentUnlockSecret,
        string newUnlockSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentUnlockSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(newUnlockSecret);

        KeyringLoadedObject accessObject = await LoadRequiredLatestAsync(
            KeyringObjectKind.AccessEnvelope,
            cancellationToken);
        KeyringLoadedObject stateObject = await LoadRequiredLatestAsync(
            KeyringObjectKind.StateSnapshot,
            cancellationToken);

        KeyringAccessEnvelope currentAccess = KeyringJson.Deserialize<KeyringAccessEnvelope>(accessObject.Bytes);
        KeyringUnwrapResult unwrap = KeyringCryptography.TryUnwrapRootKey(
            currentAccess,
            currentUnlockSecret);
        if (!unwrap.Success || unwrap.KeyringRootKey is null)
        {
            throw new UnauthorizedAccessException("Keyring could not be unlocked with the supplied current master key.");
        }

        byte[] newRootKey = KeyringCryptography.GenerateKeyMaterial();
        try
        {
            KeyringStateSnapshot currentSnapshot = KeyringJson.Deserialize<KeyringStateSnapshot>(stateObject.Bytes);
            KeyringPlainState currentState = KeyringCryptography.UnprotectState(
                currentSnapshot,
                unwrap.KeyringRootKey);
            ValidateCurrentState(currentAccess, currentState);

            int nextRootEpoch = currentState.RootEpoch + 1;
            KeyringPlainState nextState = currentState with
            {
                RootEpoch = nextRootEpoch,
                StateGeneration = currentState.StateGeneration + 1,
                ParentStateHash = stateObject.Pointer.Hash
            };
            KeyringStateSnapshot nextSnapshot = KeyringCryptography.ProtectState(nextState, newRootKey);
            byte[] nextSnapshotBytes = KeyringJson.SerializeToUtf8Bytes(nextSnapshot);
            KeyringObjectPointer nextStatePointer = await _store.CommitAsync(
                KeyringObjectKind.StateSnapshot,
                nextState.StateGeneration,
                nextSnapshotBytes,
                cancellationToken);

            KeyringAccessEnvelope nextAccess = KeyringCryptography.CreateLegacyMasterAccessEnvelope(
                nextState.InstanceId,
                nextState.KeyringId,
                nextRootEpoch,
                currentAccess.Generation + 1,
                accessObject.Pointer.Hash,
                newRootKey,
                newUnlockSecret,
                KeyringBootstrapService.DefaultLegacyMasterSlotId,
                DateTimeOffset.UtcNow);
            byte[] nextAccessBytes = KeyringJson.SerializeToUtf8Bytes(nextAccess);
            KeyringObjectPointer nextAccessPointer = await _store.CommitAsync(
                KeyringObjectKind.AccessEnvelope,
                nextAccess.Generation,
                nextAccessBytes,
                cancellationToken);

            return new KeyringRotationResult(
                nextState,
                nextAccess,
                nextStatePointer,
                nextAccessPointer);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrap.KeyringRootKey);
            CryptographicOperations.ZeroMemory(newRootKey);
        }
    }

    private async Task<KeyringLoadedObject> LoadRequiredLatestAsync(
        KeyringObjectKind kind,
        CancellationToken cancellationToken)
    {
        return await _store.FindLatestValidAsync(kind, cancellationToken)
            ?? throw new InvalidOperationException($"No valid keyring {kind} object exists.");
    }

    private static void ValidateCurrentState(KeyringAccessEnvelope access, KeyringPlainState state)
    {
        if (access.InstanceId != state.InstanceId
            || access.KeyringId != state.KeyringId
            || access.RootEpoch != state.RootEpoch)
        {
            throw new InvalidDataException("Current keyring access envelope and state snapshot do not match.");
        }
    }
}

internal sealed record KeyringRotationResult(
    KeyringPlainState State,
    KeyringAccessEnvelope AccessEnvelope,
    KeyringObjectPointer StatePointer,
    KeyringObjectPointer AccessPointer);
