// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Computes and verifies row MACs using descriptor-produced canonical payloads.
/// </summary>
public interface IDatabaseIntegrityProtector
{
    /// <summary>Signs a protected entity with the current master-key-derived integrity key.</summary>
    byte[] Sign(object entity, IDatabaseIntegrityDescriptor descriptor);

    /// <summary>Verifies a protected entity against a MAC stored in the database row.</summary>
    bool Verify(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);

    /// <summary>Throws when a protected entity does not match its stored MAC.</summary>
    void RequireValid(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);
}
