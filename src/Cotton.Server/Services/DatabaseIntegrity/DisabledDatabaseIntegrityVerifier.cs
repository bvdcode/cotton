// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Accepts protected rows while database integrity enforcement is disabled.
    /// </summary>
    public class DisabledDatabaseIntegrityVerifier : IDatabaseIntegrityVerifier
    {
        /// <inheritdoc />
        public void RequireValid<TEntity>(
            CottonDbContext dbContext,
            TEntity entity,
            string boundary)
            where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentException.ThrowIfNullOrWhiteSpace(boundary);
        }
    }
}
