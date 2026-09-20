// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class SessionValidationConnectionInterceptor(Action<CancellationToken> onOpening) : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            onOpening(cancellationToken);
            throw new InvalidOperationException("The test must stop before opening a database connection.");
        }
    }
}
