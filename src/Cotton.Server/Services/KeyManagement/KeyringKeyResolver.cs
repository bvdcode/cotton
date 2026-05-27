// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Resolves key material from an unlocked keyring state.
/// </summary>
internal interface IKeyringKeyResolver
{
    KeyringResolvedKey GetPrimary(KeyringKeyPurpose purpose);

    KeyringResolvedKey GetById(KeyringKeyPurpose purpose, int keyId);
}

/// <summary>
/// Resolved key material. The material is a defensive copy owned by the caller.
/// </summary>
internal sealed record KeyringResolvedKey(
    int Id,
    KeyringKeyPurpose Purpose,
    string Algorithm,
    KeyringKeyStatus Status,
    KeyringKeyOrigin Origin,
    byte[] Material);

/// <summary>
/// In-memory resolver for an unlocked keyring state.
/// </summary>
internal sealed class KeyringPlainStateKeyResolver : IKeyringKeyResolver
{
    private readonly KeyringPlainState _state;
    private readonly Dictionary<(KeyringKeyPurpose Purpose, int Id), KeyringKeyRecord> _keys;

    public KeyringPlainStateKeyResolver(KeyringPlainState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Magic != KeyringFormat.PlainStateMagic || state.Schema != KeyringFormat.SchemaVersion)
        {
            throw new InvalidDataException("Unsupported keyring state.");
        }

        _state = state;
        _keys = state.Keys.ToDictionary(x => (x.Purpose, x.Id));
    }

    public KeyringResolvedKey GetPrimary(KeyringKeyPurpose purpose)
    {
        int keyId = GetPrimaryKeyId(purpose);
        KeyringResolvedKey key = GetById(purpose, keyId);
        if (key.Status != KeyringKeyStatus.EncryptDecrypt)
        {
            throw new InvalidOperationException($"Primary key {purpose}/{keyId} is not enabled for encryption.");
        }

        return key;
    }

    public KeyringResolvedKey GetById(KeyringKeyPurpose purpose, int keyId)
    {
        if (!_keys.TryGetValue((purpose, keyId), out KeyringKeyRecord? key))
        {
            throw new KeyNotFoundException($"Keyring key {purpose}/{keyId} was not found.");
        }

        if (key.Status == KeyringKeyStatus.Disabled)
        {
            throw new InvalidOperationException($"Keyring key {purpose}/{keyId} is disabled.");
        }

        return new KeyringResolvedKey(
            key.Id,
            key.Purpose,
            key.Algorithm,
            key.Status,
            key.Origin,
            Convert.FromBase64String(key.MaterialBase64));
    }

    private int GetPrimaryKeyId(KeyringKeyPurpose purpose)
    {
        return purpose switch
        {
            KeyringKeyPurpose.ChunkAead => _state.Primary.ChunkAead,
            KeyringKeyPurpose.DbFieldAead => _state.Primary.DbFieldAead,
            KeyringKeyPurpose.DbIntegrityHmac => _state.Primary.DbIntegrityHmac,
            KeyringKeyPurpose.PreviewTokenHmac => _state.Primary.PreviewTokenHmac,
            KeyringKeyPurpose.TotpSecretAead => _state.Primary.TotpSecretAead,
            KeyringKeyPurpose.S3SecretAead => _state.Primary.S3SecretAead,
            KeyringKeyPurpose.PasswordPepper => _state.Primary.PasswordPepper,
            KeyringKeyPurpose.BackupNamespace => _state.Primary.BackupNamespace,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };
    }
}
