// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services.DatabaseIntegrity;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class UnexpectedDatabaseIntegrityVerifier : IDatabaseIntegrityVerifier
    {
        public void RequireValid<TEntity>(CottonDbContext dbContext, TEntity entity, string boundary)
            where TEntity : class
        {
            throw new InvalidOperationException("No database rows should be read during this test.");
        }
    }
}
