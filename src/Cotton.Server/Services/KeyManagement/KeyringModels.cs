// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Constants for the Cotton keyring v2 wire format.
/// </summary>
internal static class KeyringFormat
{
    public const int SchemaVersion = 2;
    public const int KeySizeBytes = 32;
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;
    public const string AccessEnvelopeMagic = "cotton.keyring-access.v2";
    public const string StateSnapshotMagic = "cotton.keyring-state.v2";
    public const string PlainStateMagic = "cotton.keyring-plain-state.v2";
    public const string Aes256Gcm = "AES-256-GCM";
    public const string HmacSha256 = "HMAC-SHA256";
    public const string HkdfSha256 = "HKDF-SHA256";
}

/// <summary>
/// Domain-separated key purposes tracked by the keyring.
/// </summary>
internal enum KeyringKeyPurpose
{
    ChunkAead,
    DbFieldAead,
    DbIntegrityHmac,
    PreviewTokenHmac,
    TotpSecretAead,
    S3SecretAead,
    PasswordPepper,
    BackupNamespace
}

/// <summary>
/// Runtime usability state for a keyring key.
/// </summary>
internal enum KeyringKeyStatus
{
    EncryptDecrypt,
    DecryptOnly,
    VerifyOnly,
    Disabled
}

/// <summary>
/// Describes where key material came from.
/// </summary>
internal enum KeyringKeyOrigin
{
    LegacyV1MasterDerived,
    RandomV2
}

/// <summary>
/// Metadata for deriving a recipient slot key-encryption key.
/// </summary>
internal sealed record KeyringKdfDescriptor(
    string Algorithm,
    string SaltBase64,
    string Info,
    int? MemoryKiB = null,
    int? Passes = null,
    int? Parallelism = null);

/// <summary>
/// A single unlock recipient that wraps the random keyring root key.
/// </summary>
internal sealed record KeyringRecipientSlot(
    string SlotId,
    string Type,
    KeyringKdfDescriptor Kdf,
    string WrapAlgorithm,
    string NonceBase64,
    string WrappedKeyringRootKeyBase64);

/// <summary>
/// Unlock envelope. It does not contain data keys; it only wraps the keyring root key for recipients.
/// </summary>
internal sealed record KeyringAccessEnvelope(
    string Magic,
    Guid InstanceId,
    string KeyringId,
    int RootEpoch,
    int Generation,
    string? ParentHash,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<KeyringRecipientSlot> Recipients);

/// <summary>
/// One encrypted state snapshot.
/// </summary>
internal sealed record KeyringStateSnapshot(
    string Magic,
    Guid InstanceId,
    string KeyringId,
    int RootEpoch,
    int StateGeneration,
    string? ParentStateHash,
    string Algorithm,
    string NonceBase64,
    string CiphertextBase64);

/// <summary>
/// Primary key ids for each purpose.
/// </summary>
internal sealed record KeyringPrimaryKeys(
    int ChunkAead,
    int DbFieldAead,
    int DbIntegrityHmac,
    int PreviewTokenHmac,
    int TotpSecretAead,
    int S3SecretAead,
    int PasswordPepper,
    int BackupNamespace);

/// <summary>
/// One key tracked inside the encrypted keyring state.
/// </summary>
internal sealed record KeyringKeyRecord(
    int Id,
    KeyringKeyPurpose Purpose,
    string Algorithm,
    KeyringKeyStatus Status,
    KeyringKeyOrigin Origin,
    string MaterialBase64,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Plaintext keyring state. This type must only exist after successful unlock.
/// </summary>
internal sealed record KeyringPlainState(
    string Magic,
    int Schema,
    Guid InstanceId,
    string KeyringId,
    int RootEpoch,
    int StateGeneration,
    string? ParentStateHash,
    DateTimeOffset CreatedAtUtc,
    KeyringPrimaryKeys Primary,
    IReadOnlyList<KeyringKeyRecord> Keys);

