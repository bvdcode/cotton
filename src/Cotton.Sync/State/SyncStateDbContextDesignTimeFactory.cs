// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cotton.Sync.State
{

    /// <summary>
    /// Creates a design-time synchronization state context for EF Core tooling.
    /// </summary>
    public sealed class SyncStateDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SyncStateDbContext>
    {
        /// <inheritdoc />
        public SyncStateDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SyncStateDbContext>()
                .UseSqlite("Data Source=cotton-sync-design-time.sqlite")
                .Options;
            return new SyncStateDbContext(options);
        }
    }
}
