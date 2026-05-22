// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Verifies protected tracked entities before they are trusted by sensitive application flows.
/// </summary>
public interface IDatabaseIntegrityVerifier
{
    /// <summary>Requires the supplied tracked entity to have current and valid integrity metadata.</summary>
    void RequireValid<TEntity>(
        CottonDbContext dbContext,
        TEntity entity,
        string boundary)
        where TEntity : class;
}
