// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class SaveChangesProbe(Func<CancellationToken, Task>? beforeSave = null) : SaveChangesInterceptor
    {
        public int SaveCount { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (beforeSave is not null)
            {
                await beforeSave(cancellationToken);
            }
            return result;
        }
    }
}
