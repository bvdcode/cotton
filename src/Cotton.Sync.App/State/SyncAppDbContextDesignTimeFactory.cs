// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cotton.Sync.App.State
{
    internal sealed class SyncAppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SyncAppDbContext>
    {
        public SyncAppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SyncAppDbContext>()
                .UseSqlite("Data Source=cotton-sync-app-design-time.sqlite")
                .Options;
            return new SyncAppDbContext(options);
        }
    }
}
