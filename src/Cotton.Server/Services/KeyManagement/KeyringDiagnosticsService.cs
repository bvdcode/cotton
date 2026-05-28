// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Verifies keyring v2 objects and summarizes their migration/security state.
/// </summary>
public sealed class KeyringDiagnosticsService
{
    private readonly KeyringJournaledObjectStore _store;

    internal KeyringDiagnosticsService(KeyringJournaledObjectStore store)
    {
        _store = store;
    }

    internal async Task<KeyringDiagnosticsSnapshot> GetSnapshotAsync(
        string? unlockSecret = null,
        CancellationToken cancellationToken = default)
    {
        KeyringLoadedObject? accessObject = await _store.FindLatestValidAsync(
            KeyringObjectKind.AccessEnvelope,
            cancellationToken);
        KeyringLoadedObject? stateObject = await _store.FindLatestValidAsync(
            KeyringObjectKind.StateSnapshot,
            cancellationToken);
        List<string> warnings = [];

        if (accessObject is null)
        {
            warnings.Add("keyring-access-missing");
        }

        if (stateObject is null)
        {
            warnings.Add("keyring-state-missing");
        }

        KeyringAccessEnvelope? access = accessObject is null
            ? null
            : KeyringJson.Deserialize<KeyringAccessEnvelope>(accessObject.Bytes);
        KeyringStateSnapshot? snapshot = stateObject is null
            ? null
            : KeyringJson.Deserialize<KeyringStateSnapshot>(stateObject.Bytes);
        int? recipientCount = access?.Recipients.Count;
        int? recoverySlotCount = access?.Recipients.Count(x => x.Type == KeyringCryptography.RecoverySlotType);
        if (access is not null && recoverySlotCount == 0)
        {
            warnings.Add("keyring-recovery-missing");
        }

        KeyringPlainState? state = null;
        bool? unlockSucceeded = null;
        string? unlockedSlotId = null;
        if (!string.IsNullOrWhiteSpace(unlockSecret) && access is not null && snapshot is not null)
        {
            (unlockSucceeded, unlockedSlotId, state) = TryUnlock(access, snapshot, unlockSecret);
            if (unlockSucceeded != true)
            {
                warnings.Add("keyring-unlock-failed");
            }
        }

        if (access is not null && snapshot is not null
            && (access.InstanceId != snapshot.InstanceId
                || access.KeyringId != snapshot.KeyringId
                || access.RootEpoch != snapshot.RootEpoch))
        {
            warnings.Add("keyring-access-state-mismatch");
        }

        int legacyDecryptOnlyKeys = state?.Keys.Count(x =>
            x.Origin == KeyringKeyOrigin.LegacyV1MasterDerived
            && x.Status is KeyringKeyStatus.DecryptOnly or KeyringKeyStatus.VerifyOnly) ?? 0;
        if (legacyDecryptOnlyKeys > 0)
        {
            warnings.Add("keyring-legacy-debt");
        }

        return new KeyringDiagnosticsSnapshot(
            AccessEnvelopePresent: accessObject is not null,
            StateSnapshotPresent: stateObject is not null,
            AccessGeneration: access?.Generation,
            StateGeneration: snapshot?.StateGeneration,
            RootEpoch: snapshot?.RootEpoch ?? access?.RootEpoch,
            UnlockSucceeded: unlockSucceeded,
            UnlockedSlotId: unlockedSlotId,
            RecipientCount: recipientCount,
            RecoverySlotCount: recoverySlotCount,
            KeyCount: state?.Keys.Count,
            LegacyDecryptOnlyKeyCount: state is null ? null : legacyDecryptOnlyKeys,
            Warnings: warnings);
    }

    private static (bool Succeeded, string? SlotId, KeyringPlainState? State) TryUnlock(
        KeyringAccessEnvelope access,
        KeyringStateSnapshot snapshot,
        string unlockSecret)
    {
        KeyringUnwrapResult unwrap = KeyringCryptography.TryUnwrapRootKey(access, unlockSecret);
        if (!unwrap.Success || unwrap.KeyringRootKey is null)
        {
            return (false, null, null);
        }

        try
        {
            KeyringPlainState state = KeyringCryptography.UnprotectState(snapshot, unwrap.KeyringRootKey);
            return (true, unwrap.SlotId, state);
        }
        catch (CryptographicException)
        {
            return (false, unwrap.SlotId, null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrap.KeyringRootKey);
        }
    }
}

internal sealed record KeyringDiagnosticsSnapshot(
    bool AccessEnvelopePresent,
    bool StateSnapshotPresent,
    int? AccessGeneration,
    int? StateGeneration,
    int? RootEpoch,
    bool? UnlockSucceeded,
    string? UnlockedSlotId,
    int? RecipientCount,
    int? RecoverySlotCount,
    int? KeyCount,
    int? LegacyDecryptOnlyKeyCount,
    IReadOnlyList<string> Warnings);
