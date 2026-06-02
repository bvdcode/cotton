// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cotton.Sync.ClientState;

/// <summary>
/// Creates a design-time client state context for EF Core tooling.
/// </summary>
public sealed class ClientStateDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ClientStateDbContext>
{
    /// <inheritdoc />
    public ClientStateDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ClientStateDbContext>()
            .UseSqlite("Data Source=cotton-sync-client-state-design-time.sqlite")
            .Options;
        return new ClientStateDbContext(options);
    }
}
