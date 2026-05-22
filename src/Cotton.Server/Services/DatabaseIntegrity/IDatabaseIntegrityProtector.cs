// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Signs and verifies protected entity payloads with the database-integrity HMAC key.
/// </summary>
public interface IDatabaseIntegrityProtector
{
    /// <summary>Computes the MAC for one entity using its descriptor.</summary>
    byte[] Sign(object entity, IDatabaseIntegrityDescriptor descriptor);
    /// <summary>Compares the expected row MAC with a freshly computed MAC in constant time.</summary>
    bool Verify(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);
    /// <summary>Throws <see cref="DatabaseIntegrityException"/> when a protected row MAC does not verify.</summary>
    void RequireValid(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);
}
