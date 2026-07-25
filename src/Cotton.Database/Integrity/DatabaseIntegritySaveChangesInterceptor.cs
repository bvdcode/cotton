// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Database.Integrity
{
    internal class DatabaseIntegritySaveChangesInterceptor(
        IDatabaseIntegrityChangeSigner integrityChangeSigner) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            SignPendingChanges(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SignPendingChanges(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void SignPendingChanges(DbContext? dbContext)
        {
            if (dbContext is not null)
            {
                integrityChangeSigner.SignPendingChanges(dbContext);
            }
        }
    }
}
