// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class RejectDeletionSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<NodeFile>().Any(x => x.State == EntityState.Deleted))
            {
                throw new InvalidOperationException("Deletion save rejected.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
