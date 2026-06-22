// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Verifies protected tracked entities at read boundaries that grant access or trust data.
/// </summary>
public interface IDatabaseIntegrityVerifier
{
    /// <summary>
    /// Requires the supplied tracked entity to match its stored integrity metadata.
    /// </summary>
    /// <typeparam name="TEntity">The EF entity type being checked.</typeparam>
    /// <param name="dbContext">The context tracking the entity, used to read shadow integrity properties.</param>
    /// <param name="entity">The protected entity instance.</param>
    /// <param name="boundary">A short name for the read boundary, used in logs and admin notifications.</param>
    void RequireValid<TEntity>(
        CottonDbContext dbContext,
        TEntity entity,
        string boundary)
        where TEntity : class;
}
