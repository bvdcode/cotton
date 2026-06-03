// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.App.SyncPairs;

internal sealed class SyncPairSettingsDbContext : DbContext
{
    public SyncPairSettingsDbContext(DbContextOptions<SyncPairSettingsDbContext> options)
        : base(options)
    {
    }

    public DbSet<SyncPairSettingsEntity> SyncPairSettings => Set<SyncPairSettingsEntity>();
}
