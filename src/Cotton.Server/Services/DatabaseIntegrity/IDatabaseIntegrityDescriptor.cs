// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.DatabaseIntegrity
{
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
        Type EntityType { get; }

        string EntityName { get; }

        int SchemaVersion { get; }

        string GetEntityKey(object entity);

        byte[] BuildCanonicalPayload(object entity);

        Task<int> CountInvalidMetadataRowsAsync(
            DbContext dbContext,
            CancellationToken cancellationToken);
    }

    public interface IDatabaseIntegrityDescriptor<in T> : IDatabaseIntegrityDescriptor
        where T : class
    {
        string GetEntityKey(T entity);

        /// <summary>
        /// Writes the security-sensitive domain fields for the entity in deterministic order.
        /// </summary>
        void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, T entity);
    }
}
