// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.ClientState;

/// <summary>
/// Entity Framework context for sync-client profile state.
/// </summary>
public sealed class ClientStateDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientStateDbContext" /> class.
    /// </summary>
    public ClientStateDbContext(DbContextOptions<ClientStateDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets client profile state rows.
    /// </summary>
    public DbSet<ClientStateItem> StateItems => Set<ClientStateItem>();
}
