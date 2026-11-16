// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Cotton.Server.Services
{
    public class PublicShareExecutionStrategy(CottonDbContext database) : NpgsqlRetryingExecutionStrategy(database)
    {
        protected override bool ShouldRetryOn(Exception exception)
        {
            if (exception is InvalidOperationException { InnerException: { } innerException })
            {
                return CallOnWrappedException(innerException, base.ShouldRetryOn);
            }
            return base.ShouldRetryOn(exception);
        }
    }
}
