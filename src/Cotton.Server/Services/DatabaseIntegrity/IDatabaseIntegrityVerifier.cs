// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;

namespace Cotton.Server.Services.DatabaseIntegrity;

public interface IDatabaseIntegrityVerifier
{
    void RequireValid<TEntity>(
        CottonDbContext dbContext,
        TEntity entity,
        string boundary)
        where TEntity : class;
}
