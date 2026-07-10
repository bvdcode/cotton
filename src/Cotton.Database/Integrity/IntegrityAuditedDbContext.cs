// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Database.Integrity
{
    /// <summary>
    /// Adds database-integrity model metadata and automatic signing to an audited EF context.
    /// </summary>
    public abstract class IntegrityAuditedDbContext(
        DbContextOptions options,
        IDatabaseIntegrityChangeSigner? integrityChangeSigner) : AuditedDbContext(options)
    {
        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            if (integrityChangeSigner is not null)
            {
                optionsBuilder.AddInterceptors(
                    new DatabaseIntegritySaveChangesInterceptor(integrityChangeSigner));
            }
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            DatabaseIntegrityModelConfiguration.Configure(modelBuilder);
        }
    }
}
