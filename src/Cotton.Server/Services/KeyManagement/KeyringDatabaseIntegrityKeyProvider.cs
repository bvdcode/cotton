// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.DatabaseIntegrity;
using System.Security.Cryptography;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Database-integrity HMAC provider backed by keyring v2 key material.
/// </summary>
internal sealed class KeyringDatabaseIntegrityKeyProvider(KeyringPlainState _state) :
    IDatabaseIntegrityKeyProvider,
    IDatabaseIntegrityVerificationKeyProvider
{
    public HMACSHA256 CreateHmac()
    {
        KeyringKeyRecord primary = FindPrimarySigningKey();
        return CreateHmac(primary);
    }

    public IReadOnlyList<HMACSHA256> CreateVerificationHmacs()
    {
        List<KeyringKeyRecord> records = _state.Keys
            .Where(x => x.Purpose == KeyringKeyPurpose.DbIntegrityHmac)
            .Where(x => x.Status is KeyringKeyStatus.EncryptDecrypt or KeyringKeyStatus.VerifyOnly)
            .OrderBy(x => x.Id == _state.Primary.DbIntegrityHmac ? 0 : 1)
            .ThenBy(x => x.Id)
            .ToList();
        if (records.Count == 0)
        {
            throw new InvalidOperationException("Keyring has no database-integrity verification keys.");
        }

        return records.Select(CreateHmac).ToArray();
    }

    private KeyringKeyRecord FindPrimarySigningKey()
    {
        KeyringKeyRecord? record = _state.Keys.SingleOrDefault(x =>
            x.Purpose == KeyringKeyPurpose.DbIntegrityHmac
            && x.Id == _state.Primary.DbIntegrityHmac);
        if (record is null)
        {
            throw new KeyNotFoundException(
                $"Keyring primary database-integrity key {_state.Primary.DbIntegrityHmac} is missing.");
        }

        if (record.Status != KeyringKeyStatus.EncryptDecrypt)
        {
            throw new InvalidOperationException(
                $"Keyring database-integrity key {record.Id} is not enabled for signing.");
        }

        return record;
    }

    private static HMACSHA256 CreateHmac(KeyringKeyRecord record)
    {
        if (record.Algorithm != KeyringFormat.HmacSha256)
        {
            throw new InvalidOperationException(
                $"Keyring database-integrity key {record.Id} uses unsupported algorithm {record.Algorithm}.");
        }

        byte[] material = Convert.FromBase64String(record.MaterialBase64);
        try
        {
            if (material.Length != DatabaseIntegrityKeyProvider.KeySizeBytes)
            {
                throw new InvalidDataException(
                    $"Keyring database-integrity key {record.Id} has invalid material size.");
            }

            return new HMACSHA256(material);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }
}
