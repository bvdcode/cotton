// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.State
{
    /// <summary>
    /// Entity Framework context for local synchronization state.
    /// </summary>
    public sealed class SyncStateDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncStateDbContext" /> class.
        /// </summary>
        public SyncStateDbContext(DbContextOptions<SyncStateDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets persisted synchronization baseline entries.
        /// </summary>
        public DbSet<SyncStateEntity> SyncEntries => Set<SyncStateEntity>();

        /// <summary>
        /// Gets persisted remote change-feed checkpoints.
        /// </summary>
        public DbSet<SyncChangeCursorEntity> SyncChangeCursors => Set<SyncChangeCursorEntity>();
    }
}
