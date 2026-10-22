// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class RejectSecondMetadataBatchInterceptor : SaveChangesInterceptor
    {
        public int SaveAttempts { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Node>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.Metadata?.ContainsKey("category") == true))
            {
                SaveAttempts++;
                if (SaveAttempts == 2)
                {
                    throw new InvalidOperationException("Second metadata batch rejected.");
                }
            }
            return ValueTask.FromResult(result);
        }
    }
}
