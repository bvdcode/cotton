// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class MetadataPersistenceFailureInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowIfEnabled(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfEnabled(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void ThrowIfEnabled(DbContext? dbContext)
        {
            if (!Enabled || dbContext is null)
            {
                return;
            }

            bool isMetadataUpdate = dbContext.ChangeTracker
                .Entries<FileManifest>()
                .Any(IsMetadataUpdate);
            if (isMetadataUpdate)
            {
                throw new DbUpdateException("Simulated file metadata persistence failure.");
            }
        }

        private static bool IsMetadataUpdate(EntityEntry<FileManifest> entry)
        {
            return entry.State == EntityState.Modified
                && entry.Property(manifest => manifest.Metadata).IsModified
                && entry.Entity.Metadata is not null;
        }
    }
}
