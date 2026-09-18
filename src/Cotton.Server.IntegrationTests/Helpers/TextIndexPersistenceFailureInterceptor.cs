// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class TextIndexPersistenceFailureInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<FileManifest>()
                .Any(entry => entry.State == EntityState.Modified && entry.Property(file => file.TextIndexVersion).IsModified))
            {
                throw new DbUpdateException("Text index persistence failed.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
