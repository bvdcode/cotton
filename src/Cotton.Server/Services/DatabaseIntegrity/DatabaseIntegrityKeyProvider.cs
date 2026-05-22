// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityKeyProvider : IDatabaseIntegrityKeyProvider, IDisposable
{
    public const string Purpose = "CottonDbIntegrityKey:v1";
    public const int KeySizeBytes = 32;

    private readonly byte[] _key;
    private bool _disposed;

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

    public HMACSHA256 CreateHmac()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new HMACSHA256(_key);
    }

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
