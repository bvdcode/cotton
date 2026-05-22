// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Computes and verifies row integrity MACs over descriptor-produced canonical payloads.
/// </summary>
public sealed class DatabaseIntegrityProtector(IDatabaseIntegrityKeyProvider keyProvider) : IDatabaseIntegrityProtector
{
    /// <inheritdoc />
    public byte[] Sign(object entity, IDatabaseIntegrityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(descriptor);

        byte[] payload = descriptor.BuildCanonicalPayload(entity);
        try
        {
            using HMACSHA256 hmac = keyProvider.CreateHmac();
            return hmac.ComputeHash(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    /// <inheritdoc />
    public bool Verify(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac)
    {
        ArgumentNullException.ThrowIfNull(expectedMac);

        byte[] actualMac = Sign(entity, descriptor);
        try
        {
            return CryptographicOperations.FixedTimeEquals(actualMac, expectedMac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualMac);
        }
    }

    /// <inheritdoc />
    public void RequireValid(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac)
    {
        if (Verify(entity, descriptor, expectedMac))
        {
            return;
        }

        throw new DatabaseIntegrityException(descriptor.EntityName, descriptor.GetEntityKey(entity));
    }
}
