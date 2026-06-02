// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cotton.Sync.Cli.Storage;

/// <summary>
/// Creates a design-time CLI state context for EF Core tooling.
/// </summary>
public sealed class CliStateDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CliStateDbContext>
{
    /// <inheritdoc />
    public CliStateDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CliStateDbContext>()
            .UseSqlite("Data Source=cotton-sync-cli-design-time.sqlite")
            .Options;
        return new CliStateDbContext(options);
    }
}
