// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Describes one protected EF entity type and how to turn each row into a stable payload for integrity signing.
/// </summary>
/// <remarks>
/// Descriptors are deliberately explicit: every protected column is written in a fixed order so database tampering
/// changes the HMAC input. Adding or removing protected fields requires a new schema version.
/// </remarks>
public interface IDatabaseIntegrityDescriptor
{
    /// <summary>Gets the CLR entity type handled by this descriptor.</summary>
    Type EntityType { get; }
    /// <summary>Gets the stable logical entity name included in the signed payload.</summary>
    string EntityName { get; }
    /// <summary>Gets the descriptor schema version expected in the row integrity metadata.</summary>
    int SchemaVersion { get; }
    /// <summary>Returns a stable human-readable row key for diagnostics and integrity failure notifications.</summary>
    string GetEntityKey(object entity);
    /// <summary>Builds the deterministic byte payload that is signed for the supplied entity instance.</summary>
    byte[] BuildCanonicalPayload(object entity);
}

/// <summary>
/// Type-safe descriptor contract used by concrete protected-entity descriptors.
/// </summary>
/// <typeparam name="T">The EF entity type represented by the descriptor.</typeparam>
public interface IDatabaseIntegrityDescriptor<in T> : IDatabaseIntegrityDescriptor
{
    /// <summary>Returns the stable diagnostics key for a typed entity instance.</summary>
    string GetEntityKey(T entity);
    /// <summary>Writes the protected fields in canonical order.</summary>
    void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, T entity);
}
