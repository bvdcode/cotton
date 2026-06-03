// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.App.State;

internal sealed class SyncAppDbContext : DbContext
{
    public SyncAppDbContext(DbContextOptions<SyncAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppPreferencesEntity> AppPreferences => Set<AppPreferencesEntity>();

    public DbSet<SyncPairSettingsEntity> SyncPairSettings => Set<SyncPairSettingsEntity>();
}
