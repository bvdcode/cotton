// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Derives the database-integrity HMAC key from the Cotton master encryption key.
/// </summary>
/// <remarks>
/// The derived key is process-local and never persisted. A database-only attacker therefore cannot forge row MACs
/// unless they also obtain the master key or compromise the running process.
/// </remarks>
public sealed class DatabaseIntegrityKeyProvider : IDatabaseIntegrityKeyProvider, IDisposable
{
    /// <summary>Purpose string used to domain-separate the integrity subkey from other master-key derivations.</summary>
    public const string Purpose = "CottonDbIntegrityKey:v1";
    /// <summary>Size, in bytes, of the derived HMAC-SHA256 key.</summary>
    public const int KeySizeBytes = 32;

    private readonly byte[] _key;
    private bool _disposed;

    /// <summary>Initializes a new integrity key provider from configured Cotton encryption settings.</summary>
    public DatabaseIntegrityKeyProvider(CottonEncryptionSettings encryptionSettings)
    {
        ArgumentNullException.ThrowIfNull(encryptionSettings);
        if (string.IsNullOrWhiteSpace(encryptionSettings.MasterEncryptionKey))
        {
            throw new InvalidOperationException("MasterEncryptionKey is not configured.");
        }

        byte[] masterKey = Convert.FromBase64String(encryptionSettings.MasterEncryptionKey);
        byte[] purpose = Encoding.UTF8.GetBytes(Purpose);
        try
        {
            _key = KeyDerivation.DeriveSubkey(masterKey, purpose, KeySizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
            CryptographicOperations.ZeroMemory(purpose);
        }
    }

    /// <inheritdoc />
    public HMACSHA256 CreateHmac()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new HMACSHA256(_key);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_key);
        _disposed = true;
    }
}
