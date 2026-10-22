// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class ManifestLinksDeletedInterceptor(Func<CancellationToken, Task> afterDeletion) : DbCommandInterceptor
    {
        public bool Invoked { get; private set; }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (!Invoked && command.CommandText.StartsWith("DELETE FROM file_manifest_chunks", StringComparison.Ordinal))
            {
                Invoked = true;
                await afterDeletion(cancellationToken);
            }
            return result;
        }
    }
}
