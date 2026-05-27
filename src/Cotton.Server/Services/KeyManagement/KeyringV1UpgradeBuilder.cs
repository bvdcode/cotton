// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Server.Services.DatabaseIntegrity;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Builds the initial keyring v2 state from the current v1 master-key-derived runtime settings.
/// </summary>
internal static class KeyringV1UpgradeBuilder
{
    public const int LegacyKeyId = 1;
    public const int FirstV2ChunkKeyId = 2;
    public const int FirstV2DbFieldKeyId = 10;
    public const int FirstV2DbIntegrityKeyId = 20;
    public const int FirstV2PreviewTokenKeyId = 30;
    public const int FirstV2TotpSecretKeyId = 40;
    public const int FirstV2S3SecretKeyId = 41;
    public const int LegacyPasswordPepperKeyId = 50;
    public const int FirstV2BackupNamespaceKeyId = 60;

    public static KeyringPlainState CreateInitialState(
        CottonEncryptionSettings legacySettings,
        Guid instanceId,
        string? keyringId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(legacySettings);
        if (string.IsNullOrWhiteSpace(legacySettings.MasterEncryptionKey))
        {
            throw new InvalidOperationException("Legacy MasterEncryptionKey is required to create a v2 keyring.");
        }
        if (string.IsNullOrWhiteSpace(legacySettings.Pepper))
        {
            throw new InvalidOperationException("Legacy Pepper is required to create a v2 keyring.");
        }

        DateTimeOffset now = createdAtUtc ?? DateTimeOffset.UtcNow;
        string resolvedKeyringId = string.IsNullOrWhiteSpace(keyringId)
            ? Guid.NewGuid().ToString("N")
            : keyringId;

        List<KeyringKeyRecord> keys =
        [
            CreateLegacyKey(LegacyKeyId, KeyringKeyPurpose.ChunkAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.DecryptOnly, legacySettings.MasterEncryptionKey, now),
            CreateRandomKey(FirstV2ChunkKeyId, KeyringKeyPurpose.ChunkAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.EncryptDecrypt, now),

            CreateLegacyKey(LegacyKeyId, KeyringKeyPurpose.DbFieldAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.DecryptOnly, legacySettings.MasterEncryptionKey, now),
            CreateRandomKey(FirstV2DbFieldKeyId, KeyringKeyPurpose.DbFieldAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.EncryptDecrypt, now),

            CreateLegacyKey(LegacyKeyId, KeyringKeyPurpose.DbIntegrityHmac, KeyringFormat.HmacSha256, KeyringKeyStatus.VerifyOnly, DeriveLegacyDatabaseIntegrityKey(legacySettings), now),
            CreateRandomKey(FirstV2DbIntegrityKeyId, KeyringKeyPurpose.DbIntegrityHmac, KeyringFormat.HmacSha256, KeyringKeyStatus.EncryptDecrypt, now),

            CreateLegacyKey(LegacyKeyId, KeyringKeyPurpose.PreviewTokenHmac, KeyringFormat.Aes256Gcm, KeyringKeyStatus.DecryptOnly, legacySettings.MasterEncryptionKey, now),
            CreateRandomKey(FirstV2PreviewTokenKeyId, KeyringKeyPurpose.PreviewTokenHmac, KeyringFormat.HmacSha256, KeyringKeyStatus.EncryptDecrypt, now),

            CreateLegacyKey(LegacyKeyId, KeyringKeyPurpose.TotpSecretAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.DecryptOnly, legacySettings.MasterEncryptionKey, now),
            CreateRandomKey(FirstV2TotpSecretKeyId, KeyringKeyPurpose.TotpSecretAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.EncryptDecrypt, now),

            CreateLegacyKey(LegacyKeyId, KeyringKeyPurpose.S3SecretAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.DecryptOnly, legacySettings.MasterEncryptionKey, now),
            CreateRandomKey(FirstV2S3SecretKeyId, KeyringKeyPurpose.S3SecretAead, KeyringFormat.Aes256Gcm, KeyringKeyStatus.EncryptDecrypt, now),

            // Keep the legacy pepper active until password/WebDAV hash migration exists.
            CreateLegacyKey(LegacyPasswordPepperKeyId, KeyringKeyPurpose.PasswordPepper, "PEPPER", KeyringKeyStatus.EncryptDecrypt, legacySettings.Pepper, now),

            CreateRandomKey(FirstV2BackupNamespaceKeyId, KeyringKeyPurpose.BackupNamespace, "HKDF-SHA256-INPUT", KeyringKeyStatus.EncryptDecrypt, now)
        ];

        keys = keys
            .OrderBy(x => x.Purpose)
            .ThenBy(x => x.Id)
            .ToList();

        return new KeyringPlainState(
            KeyringFormat.PlainStateMagic,
            KeyringFormat.SchemaVersion,
            instanceId,
            resolvedKeyringId,
            RootEpoch: 1,
            StateGeneration: 1,
            ParentStateHash: null,
            CreatedAtUtc: now,
            Primary: new KeyringPrimaryKeys(
                FirstV2ChunkKeyId,
                FirstV2DbFieldKeyId,
                FirstV2DbIntegrityKeyId,
                FirstV2PreviewTokenKeyId,
                FirstV2TotpSecretKeyId,
                FirstV2S3SecretKeyId,
                LegacyPasswordPepperKeyId,
                FirstV2BackupNamespaceKeyId),
            Keys: keys);
    }

    public static CottonEncryptionSettings CreateLegacySettingsFromState(
        KeyringPlainState state,
        int encryptionThreads = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        string masterEncryptionKey = GetLegacyMaterial(
            state,
            KeyringKeyPurpose.ChunkAead,
            LegacyKeyId);
        string pepper = GetLegacyMaterial(
            state,
            KeyringKeyPurpose.PasswordPepper,
            LegacyPasswordPepperKeyId);

        return new CottonEncryptionSettings
        {
            MasterEncryptionKey = masterEncryptionKey,
            MasterEncryptionKeyId = LegacyKeyId,
            Pepper = pepper,
            EncryptionThreads = encryptionThreads,
        };
    }

    private static string GetLegacyMaterial(
        KeyringPlainState state,
        KeyringKeyPurpose purpose,
        int keyId)
    {
        KeyringKeyRecord? key = state.Keys.FirstOrDefault(x =>
            x.Id == keyId
            && x.Purpose == purpose
            && x.Origin == KeyringKeyOrigin.LegacyV1MasterDerived);
        if (key is null || string.IsNullOrWhiteSpace(key.MaterialBase64))
        {
            throw new InvalidDataException($"Keyring state does not contain legacy {purpose} key material.");
        }

        _ = Convert.FromBase64String(key.MaterialBase64);
        return key.MaterialBase64;
    }

    private static KeyringKeyRecord CreateLegacyKey(
        int id,
        KeyringKeyPurpose purpose,
        string algorithm,
        KeyringKeyStatus status,
        string materialBase64,
        DateTimeOffset createdAtUtc)
    {
        _ = Convert.FromBase64String(materialBase64);
        return new KeyringKeyRecord(
            id,
            purpose,
            algorithm,
            status,
            KeyringKeyOrigin.LegacyV1MasterDerived,
            materialBase64,
            createdAtUtc);
    }

    private static KeyringKeyRecord CreateRandomKey(
        int id,
        KeyringKeyPurpose purpose,
        string algorithm,
        KeyringKeyStatus status,
        DateTimeOffset createdAtUtc)
    {
        byte[] key = KeyringCryptography.GenerateKeyMaterial();
        try
        {
            return new KeyringKeyRecord(
                id,
                purpose,
                algorithm,
                status,
                KeyringKeyOrigin.RandomV2,
                Convert.ToBase64String(key),
                createdAtUtc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static string DeriveLegacyDatabaseIntegrityKey(CottonEncryptionSettings legacySettings)
    {
        byte[] masterKey = Convert.FromBase64String(legacySettings.MasterEncryptionKey);
        byte[] purpose = Encoding.UTF8.GetBytes(DatabaseIntegrityKeyProvider.Purpose);
        try
        {
            byte[] key = KeyDerivation.DeriveSubkey(masterKey, purpose, DatabaseIntegrityKeyProvider.KeySizeBytes);
            try
            {
                return Convert.ToBase64String(key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
            CryptographicOperations.ZeroMemory(purpose);
        }
    }
}

