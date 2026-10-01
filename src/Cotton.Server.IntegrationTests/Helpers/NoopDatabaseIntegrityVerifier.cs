// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class NoopDatabaseIntegrityVerifier : IDatabaseIntegrityVerifier
    {
        public void RequireValid<TEntity>(CottonDbContext dbContext, TEntity entity, string boundary)
            where TEntity : class
        {
        }
    }
}
