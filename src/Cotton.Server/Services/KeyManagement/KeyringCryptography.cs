// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Cryptographic helpers for keyring v2 envelopes.
/// </summary>
internal static class KeyringCryptography
{
    public const string LegacyMasterSlotType = "legacy-master-hkdf";
    public const string RecoverySlotType = "recovery-hkdf";

    public static byte[] GenerateKeyMaterial()
    {
        return RandomNumberGenerator.GetBytes(KeyringFormat.KeySizeBytes);
    }

    public static KeyringAccessEnvelope CreateLegacyMasterAccessEnvelope(
        Guid instanceId,
        string keyringId,
        int rootEpoch,
        int generation,
        string? parentHash,
        ReadOnlySpan<byte> keyringRootKey,
        string unlockSecret,
        string slotId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyringId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unlockSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        if (keyringRootKey.Length != KeyringFormat.KeySizeBytes)
        {
            throw new ArgumentException(
                $"Keyring root key must be {KeyringFormat.KeySizeBytes} bytes.",
                nameof(keyringRootKey));
        }

        KeyringRecipientSlot slot = CreateRecipientSlot(
            instanceId,
            keyringId,
            rootEpoch,
            generation,
            keyringRootKey,
            unlockSecret,
            slotId,
            LegacyMasterSlotType);

        return new KeyringAccessEnvelope(
            KeyringFormat.AccessEnvelopeMagic,
            instanceId,
            keyringId,
            rootEpoch,
            generation,
            parentHash,
            createdAtUtc,
            [slot]);
    }

    public static KeyringRecipientSlot CreateRecoveryRecipientSlot(
        Guid instanceId,
        string keyringId,
        int rootEpoch,
        int generation,
        ReadOnlySpan<byte> keyringRootKey,
        string recoverySecret,
        string slotId)
    {
        return CreateRecipientSlot(
            instanceId,
            keyringId,
            rootEpoch,
            generation,
            keyringRootKey,
            recoverySecret,
            slotId,
            RecoverySlotType);
    }

    private static KeyringRecipientSlot CreateRecipientSlot(
        Guid instanceId,
        string keyringId,
        int rootEpoch,
        int generation,
        ReadOnlySpan<byte> keyringRootKey,
        string unlockSecret,
        string slotId,
        string slotType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyringId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unlockSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotType);
        if (keyringRootKey.Length != KeyringFormat.KeySizeBytes)
        {
            throw new ArgumentException(
                $"Keyring root key must be {KeyringFormat.KeySizeBytes} bytes.",
                nameof(keyringRootKey));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(KeyringFormat.KeySizeBytes);
        string info = BuildSlotInfo(instanceId, keyringId, rootEpoch, generation, slotId, slotType);
        var kdf = new KeyringKdfDescriptor(
            KeyringFormat.HkdfSha256,
            Convert.ToBase64String(salt),
            info);

        byte[] kek = DeriveSlotKek(unlockSecret, kdf);
        byte[] nonce = RandomNumberGenerator.GetBytes(KeyringFormat.NonceSizeBytes);
        byte[] ciphertext = new byte[KeyringFormat.KeySizeBytes];
        byte[] tag = new byte[KeyringFormat.TagSizeBytes];
        try
        {
            byte[] aad = BuildAccessSlotAad(
                instanceId,
                keyringId,
                rootEpoch,
                generation,
                slotId,
                slotType,
                kdf);
            try
            {
                using var aes = new AesGcm(kek, KeyringFormat.TagSizeBytes);
                aes.Encrypt(nonce, keyringRootKey, ciphertext, tag, aad);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(aad);
            }

            return new KeyringRecipientSlot(
                slotId,
                slotType,
                kdf,
                KeyringFormat.Aes256Gcm,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(Concat(ciphertext, tag)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    public static KeyringUnwrapResult TryUnwrapRootKey(
        KeyringAccessEnvelope envelope,
        string unlockSecret)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(unlockSecret);
        if (envelope.Magic != KeyringFormat.AccessEnvelopeMagic)
        {
            throw new InvalidDataException("Unsupported keyring access envelope.");
        }

        foreach (KeyringRecipientSlot slot in envelope.Recipients)
        {
            if (!IsSupportedSlotType(slot.Type)
                || slot.WrapAlgorithm != KeyringFormat.Aes256Gcm
                || slot.Kdf.Algorithm != KeyringFormat.HkdfSha256)
            {
                continue;
            }

            byte[]? rootKey = TryUnwrapSlot(envelope, slot, unlockSecret);
            if (rootKey is not null)
            {
                return new KeyringUnwrapResult(true, rootKey, slot.SlotId);
            }
        }

        return new KeyringUnwrapResult(false, null, null);
    }

    public static KeyringStateSnapshot ProtectState(
        KeyringPlainState state,
        ReadOnlySpan<byte> keyringRootKey)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Magic != KeyringFormat.PlainStateMagic || state.Schema != KeyringFormat.SchemaVersion)
        {
            throw new InvalidDataException("Unsupported keyring state.");
        }

        byte[] stateKey = DeriveStateAeadKey(keyringRootKey, state.InstanceId, state.KeyringId, state.RootEpoch);
        byte[] plaintext = KeyringJson.SerializeToUtf8Bytes(state);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[KeyringFormat.TagSizeBytes];
        byte[] nonce = RandomNumberGenerator.GetBytes(KeyringFormat.NonceSizeBytes);
        try
        {
            byte[] aad = BuildStateSnapshotAad(
                state.InstanceId,
                state.KeyringId,
                state.RootEpoch,
                state.StateGeneration,
                state.ParentStateHash,
                KeyringFormat.Aes256Gcm);
            try
            {
                using var aes = new AesGcm(stateKey, KeyringFormat.TagSizeBytes);
                aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(aad);
            }

            return new KeyringStateSnapshot(
                KeyringFormat.StateSnapshotMagic,
                state.InstanceId,
                state.KeyringId,
                state.RootEpoch,
                state.StateGeneration,
                state.ParentStateHash,
                KeyringFormat.Aes256Gcm,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(Concat(ciphertext, tag)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(stateKey);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(nonce);
        }
    }

    public static KeyringPlainState UnprotectState(
        KeyringStateSnapshot snapshot,
        ReadOnlySpan<byte> keyringRootKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Magic != KeyringFormat.StateSnapshotMagic
            || snapshot.Algorithm != KeyringFormat.Aes256Gcm)
        {
            throw new InvalidDataException("Unsupported keyring state snapshot.");
        }

        byte[] stateKey = DeriveStateAeadKey(
            keyringRootKey,
            snapshot.InstanceId,
            snapshot.KeyringId,
            snapshot.RootEpoch);
        byte[] nonce = Convert.FromBase64String(snapshot.NonceBase64);
        byte[] protectedBytes = Convert.FromBase64String(snapshot.CiphertextBase64);
        SplitCiphertextAndTag(protectedBytes, out ReadOnlySpan<byte> ciphertext, out ReadOnlySpan<byte> tag);
        byte[] plaintext = new byte[ciphertext.Length];
        try
        {
            byte[] aad = BuildStateSnapshotAad(
                snapshot.InstanceId,
                snapshot.KeyringId,
                snapshot.RootEpoch,
                snapshot.StateGeneration,
                snapshot.ParentStateHash,
                snapshot.Algorithm);
            try
            {
                using var aes = new AesGcm(stateKey, KeyringFormat.TagSizeBytes);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(aad);
            }

            KeyringPlainState state = KeyringJson.Deserialize<KeyringPlainState>(plaintext);
            ValidateSnapshotMatchesState(snapshot, state);
            return state;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(stateKey);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(protectedBytes);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static bool IsSupportedSlotType(string slotType)
    {
        return slotType is LegacyMasterSlotType or RecoverySlotType;
    }

    private static byte[]? TryUnwrapSlot(
        KeyringAccessEnvelope envelope,
        KeyringRecipientSlot slot,
        string unlockSecret)
    {
        byte[] kek = DeriveSlotKek(unlockSecret, slot.Kdf);
        byte[] nonce = Convert.FromBase64String(slot.NonceBase64);
        byte[] protectedBytes = Convert.FromBase64String(slot.WrappedKeyringRootKeyBase64);
        SplitCiphertextAndTag(protectedBytes, out ReadOnlySpan<byte> ciphertext, out ReadOnlySpan<byte> tag);
        byte[] rootKey = new byte[ciphertext.Length];
        try
        {
            byte[] aad = BuildAccessSlotAad(
                envelope.InstanceId,
                envelope.KeyringId,
                envelope.RootEpoch,
                envelope.Generation,
                slot.SlotId,
                slot.Type,
                slot.Kdf);
            try
            {
                using var aes = new AesGcm(kek, KeyringFormat.TagSizeBytes);
                aes.Decrypt(nonce, ciphertext, tag, rootKey, aad);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(aad);
            }

            if (rootKey.Length != KeyringFormat.KeySizeBytes)
            {
                CryptographicOperations.ZeroMemory(rootKey);
                return null;
            }

            return rootKey;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(rootKey);
            return null;
        }
        catch (FormatException)
        {
            CryptographicOperations.ZeroMemory(rootKey);
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    private static byte[] DeriveSlotKek(string unlockSecret, KeyringKdfDescriptor kdf)
    {
        byte[] secret = Encoding.UTF8.GetBytes(unlockSecret);
        byte[] salt = Convert.FromBase64String(kdf.SaltBase64);
        byte[] info = Encoding.UTF8.GetBytes(kdf.Info);
        try
        {
            return KeyDerivation.DeriveSubkey(secret, info, KeyringFormat.KeySizeBytes, salt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(info);
        }
    }

    private static byte[] DeriveStateAeadKey(
        ReadOnlySpan<byte> keyringRootKey,
        Guid instanceId,
        string keyringId,
        int rootEpoch)
    {
        byte[] info = Encoding.UTF8.GetBytes(
            $"cotton/keyring-state-aead/v2|{instanceId:D}|{keyringId}|root:{rootEpoch}");
        try
        {
            return KeyDerivation.DeriveSubkey(keyringRootKey, info, KeyringFormat.KeySizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(info);
        }
    }

    private static string BuildSlotInfo(
        Guid instanceId,
        string keyringId,
        int rootEpoch,
        int generation,
        string slotId,
        string slotType)
    {
        return $"cotton/keyring-access-slot/v2|{instanceId:D}|{keyringId}|root:{rootEpoch}|gen:{generation}|{slotType}|{slotId}";
    }

    private static byte[] BuildAccessSlotAad(
        Guid instanceId,
        string keyringId,
        int rootEpoch,
        int generation,
        string slotId,
        string slotType,
        KeyringKdfDescriptor kdf)
    {
        string aad =
            $"{KeyringFormat.AccessEnvelopeMagic}|{instanceId:D}|{keyringId}|root:{rootEpoch}|gen:{generation}|slot:{slotId}|type:{slotType}|kdf:{kdf.Algorithm}|salt:{kdf.SaltBase64}|info:{kdf.Info}";
        return Encoding.UTF8.GetBytes(aad);
    }

    private static byte[] BuildStateSnapshotAad(
        Guid instanceId,
        string keyringId,
        int rootEpoch,
        int stateGeneration,
        string? parentStateHash,
        string algorithm)
    {
        string aad =
            $"{KeyringFormat.StateSnapshotMagic}|{instanceId:D}|{keyringId}|root:{rootEpoch}|state:{stateGeneration}|parent:{parentStateHash ?? string.Empty}|alg:{algorithm}";
        return Encoding.UTF8.GetBytes(aad);
    }

    private static void ValidateSnapshotMatchesState(KeyringStateSnapshot snapshot, KeyringPlainState state)
    {
        if (state.Magic != KeyringFormat.PlainStateMagic
            || state.Schema != KeyringFormat.SchemaVersion
            || state.InstanceId != snapshot.InstanceId
            || state.KeyringId != snapshot.KeyringId
            || state.RootEpoch != snapshot.RootEpoch
            || state.StateGeneration != snapshot.StateGeneration
            || state.ParentStateHash != snapshot.ParentStateHash)
        {
            throw new InvalidDataException("Keyring snapshot metadata does not match decrypted state.");
        }
    }

    private static byte[] Concat(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        byte[] result = new byte[left.Length + right.Length];
        left.CopyTo(result);
        right.CopyTo(result.AsSpan(left.Length));
        return result;
    }

    private static void SplitCiphertextAndTag(
        byte[] protectedBytes,
        out ReadOnlySpan<byte> ciphertext,
        out ReadOnlySpan<byte> tag)
    {
        if (protectedBytes.Length <= KeyringFormat.TagSizeBytes)
        {
            throw new InvalidDataException("Protected keyring payload is too short.");
        }

        ciphertext = protectedBytes.AsSpan(0, protectedBytes.Length - KeyringFormat.TagSizeBytes);
        tag = protectedBytes.AsSpan(protectedBytes.Length - KeyringFormat.TagSizeBytes, KeyringFormat.TagSizeBytes);
    }
}

internal sealed record KeyringUnwrapResult(bool Success, byte[]? KeyringRootKey, string? SlotId);

