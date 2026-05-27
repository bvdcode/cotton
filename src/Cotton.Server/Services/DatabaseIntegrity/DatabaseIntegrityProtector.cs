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
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(expectedMac);

        byte[] payload = descriptor.BuildCanonicalPayload(entity);
        try
        {
            if (keyProvider is IDatabaseIntegrityVerificationKeyProvider verificationKeyProvider)
            {
                foreach (HMACSHA256 hmac in verificationKeyProvider.CreateVerificationHmacs())
                {
                    using (hmac)
                    {
                        if (VerifyWithHmac(hmac, payload, expectedMac))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            using HMACSHA256 signingHmac = keyProvider.CreateHmac();
            return VerifyWithHmac(signingHmac, payload, expectedMac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
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

    private static bool VerifyWithHmac(HMACSHA256 hmac, byte[] payload, byte[] expectedMac)
    {
        byte[] actualMac = hmac.ComputeHash(payload);
        try
        {
            return CryptographicOperations.FixedTimeEquals(actualMac, expectedMac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualMac);
        }
    }
}
