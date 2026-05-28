// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Models.Dto;
using System.Security.Cryptography;
using System.Text.Json;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Coordinates administrator keyring operations that update the live runtime state.
/// </summary>
public sealed class KeyringAdminService
{
    private const string RecoveryKitMagic = "cotton.recovery-kit.v2";

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

    internal async Task<KeyringCreateRecoverySlotResponseDto> CreateRecoverySlotAsync(
        string currentUnlockSecret,
        string recoverySecret,
        CancellationToken cancellationToken = default)
    {
        if (!KeyringStartup.IsEnabled())
        {
            throw new InvalidOperationException("Keyring v2 is not enabled for this process.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currentUnlockSecret);
        ValidateRecoverySecret(recoverySecret);
        if (string.Equals(currentUnlockSecret, recoverySecret, StringComparison.Ordinal))
        {
            throw new ArgumentException("Recovery secret must be different from the current unlock secret.");
        }

        var rotation = new KeyringRotationService(_store);
        KeyringRecoverySlotResult added = await rotation.AddRecoverySlotAsync(
            currentUnlockSecret,
            recoverySecret,
            cancellationToken);
        var bootstrapResult = new KeyringBootstrapResult(
            Created: false,
            State: added.State,
            AccessEnvelope: added.AccessEnvelope,
            StatePointer: added.StatePointer,
            AccessPointer: added.AccessPointer,
            UnlockedSlotId: added.UnlockedSlotId);
        _runtimeState.Set(bootstrapResult);
        KeyringRecoveryKitDto recoveryKit = await ExportRecoveryKitAsync(cancellationToken);

        return new KeyringCreateRecoverySlotResponseDto
        {
            SlotId = added.SlotId,
            RootEpoch = added.State.RootEpoch,
            AccessGeneration = added.AccessEnvelope.Generation,
            StateGeneration = added.State.StateGeneration,
            RecoveryKit = recoveryKit,
        };
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

    internal async Task<KeyringRecoveryKitImportResponseDto> ImportRecoveryKitAsync(
        KeyringRecoveryKitDto kit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kit);
        if (!KeyringStartup.IsEnabled())
        {
            throw new InvalidOperationException("Keyring v2 is not enabled for this process.");
        }

        KeyringBootstrapResult runtime = _runtimeState.Current
            ?? throw new InvalidOperationException("Keyring recovery kit cannot be imported before runtime keyring is loaded.");
        ValidateRecoveryKitHeader(kit);
        byte[] accessBytes = DecodeKitObject(kit.AccessEnvelopeBase64, "access envelope");
        byte[] stateBytes = DecodeKitObject(kit.StateSnapshotBase64, "state snapshot");
        ValidateKitHash(kit.AccessEnvelopeHash, accessBytes, "access envelope");
        ValidateKitHash(kit.StateSnapshotHash, stateBytes, "state snapshot");
        ValidateObjectName(
            KeyringObjectKind.AccessEnvelope,
            kit.AccessGeneration,
            kit.AccessEnvelopeHash,
            kit.AccessEnvelopeObjectName,
            "access envelope");
        ValidateObjectName(
            KeyringObjectKind.StateSnapshot,
            kit.StateGeneration,
            kit.StateSnapshotHash,
            kit.StateSnapshotObjectName,
            "state snapshot");

        KeyringAccessEnvelope access = DeserializeAccessEnvelope(accessBytes);
        KeyringStateSnapshot snapshot = DeserializeStateSnapshot(stateBytes);
        ValidateRecoveryKitMetadata(kit, access, snapshot);
        ValidateRecoveryKitMatchesRuntime(kit, runtime);

        KeyringObjectPointer accessPointer = await _store.CommitAsync(
            KeyringObjectKind.AccessEnvelope,
            access.Generation,
            accessBytes,
            cancellationToken);
        KeyringObjectPointer statePointer = await _store.CommitAsync(
            KeyringObjectKind.StateSnapshot,
            snapshot.StateGeneration,
            stateBytes,
            cancellationToken);

        return new KeyringRecoveryKitImportResponseDto
        {
            ImportedAtUtc = DateTimeOffset.UtcNow,
            RootEpoch = access.RootEpoch,
            AccessGeneration = access.Generation,
            StateGeneration = snapshot.StateGeneration,
            AccessEnvelopeHash = accessPointer.Hash,
            StateSnapshotHash = statePointer.Hash,
        };
    }

    private static void ValidateRecoverySecret(string recoverySecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoverySecret);
        if (recoverySecret.Length != 64 || recoverySecret.Any(x => !Uri.IsHexDigit(x)))
        {
            throw new ArgumentException("Recovery secret must be a 64-character hexadecimal string.", nameof(recoverySecret));
        }
    }

    private static void ValidateRecoveryKitHeader(KeyringRecoveryKitDto kit)
    {
        if (kit.Magic != RecoveryKitMagic)
        {
            throw new InvalidOperationException("Keyring recovery kit has an invalid format marker.");
        }

        if (kit.Schema != KeyringFormat.SchemaVersion)
        {
            throw new InvalidOperationException("Keyring recovery kit uses an unsupported schema version.");
        }
    }

    private static byte[] DecodeKitObject(string base64, string objectLabel)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException($"Keyring recovery kit {objectLabel} payload is missing.");
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Keyring recovery kit {objectLabel} payload is not valid base64.", ex);
        }
    }

    private static void ValidateKitHash(string expectedHash, byte[] bytes, string objectLabel)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            throw new InvalidOperationException($"Keyring recovery kit {objectLabel} hash is missing.");
        }

        string actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Keyring recovery kit {objectLabel} hash does not match its payload.");
        }
    }

    private static void ValidateObjectName(
        KeyringObjectKind kind,
        int generation,
        string hash,
        string objectName,
        string objectLabel)
    {
        string expectedName = KeyringObjectNames.GetObjectName(kind, generation, hash.ToLowerInvariant());
        if (!string.Equals(objectName, expectedName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Keyring recovery kit {objectLabel} object name does not match its generation and hash.");
        }
    }

    private static void ValidateRecoveryKitMetadata(
        KeyringRecoveryKitDto kit,
        KeyringAccessEnvelope access,
        KeyringStateSnapshot snapshot)
    {
        if (access.InstanceId != snapshot.InstanceId
            || access.KeyringId != snapshot.KeyringId
            || access.RootEpoch != snapshot.RootEpoch)
        {
            throw new InvalidOperationException("Keyring recovery kit access and state metadata do not match.");
        }

        if (kit.InstanceId != access.InstanceId
            || kit.KeyringId != access.KeyringId
            || kit.RootEpoch != access.RootEpoch
            || kit.AccessGeneration != access.Generation
            || kit.StateGeneration != snapshot.StateGeneration)
        {
            throw new InvalidOperationException("Keyring recovery kit metadata does not match the embedded keyring objects.");
        }
    }

    private static void ValidateRecoveryKitMatchesRuntime(
        KeyringRecoveryKitDto kit,
        KeyringBootstrapResult runtime)
    {
        if (runtime.State.InstanceId != kit.InstanceId
            || runtime.State.KeyringId != kit.KeyringId
            || runtime.State.RootEpoch != kit.RootEpoch
            || runtime.State.StateGeneration != kit.StateGeneration
            || runtime.AccessEnvelope.Generation != kit.AccessGeneration
            || !string.Equals(runtime.AccessPointer.Hash, kit.AccessEnvelopeHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(runtime.StatePointer.Hash, kit.StateSnapshotHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Keyring recovery kit does not match the loaded runtime keyring.");
        }
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
