// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Describes how one protected EF entity type is converted into a canonical payload for database-integrity signing.
/// </summary>
/// <remarks>
/// A descriptor is the boundary between domain policy and cryptography. It decides which fields are security-sensitive;
/// the protector only signs the bytes it receives. Adding a field here means a database-only attacker can no longer edit
/// that field silently without also knowing the master-key-derived integrity key.
/// </remarks>
public interface IDatabaseIntegrityDescriptor
{
    /// <summary>Gets the EF entity type handled by this descriptor.</summary>
    Type EntityType { get; }

    /// <summary>Gets the stable table-like name written into the signed payload and diagnostics.</summary>
    string EntityName { get; }

    /// <summary>Gets the descriptor schema version expected in the row metadata.</summary>
    int SchemaVersion { get; }

    /// <summary>Gets the stable row key written into the signed payload and failure reports.</summary>
    string GetEntityKey(object entity);

    /// <summary>Builds the canonical binary payload that will be MACed for the entity.</summary>
    byte[] BuildCanonicalPayload(object entity);
}

/// <summary>
/// Strongly typed descriptor contract used by concrete protected entity descriptors.
/// </summary>
/// <typeparam name="T">The EF entity type represented by the descriptor.</typeparam>
public interface IDatabaseIntegrityDescriptor<in T> : IDatabaseIntegrityDescriptor
{
    /// <summary>Gets the stable row key written into the signed payload and failure reports.</summary>
    string GetEntityKey(T entity);

    /// <summary>Writes the security-sensitive domain fields for the entity in deterministic order.</summary>
    void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, T entity);
}
