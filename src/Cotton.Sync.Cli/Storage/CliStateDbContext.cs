// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.Cli.Storage;

/// <summary>
/// Entity Framework context for CLI client state.
/// </summary>
public sealed class CliStateDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CliStateDbContext" /> class.
    /// </summary>
    public CliStateDbContext(DbContextOptions<CliStateDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets CLI state rows.
    /// </summary>
    public DbSet<CliStateItem> StateItems => Set<CliStateItem>();
}
