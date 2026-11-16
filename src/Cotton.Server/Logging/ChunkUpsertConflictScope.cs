// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.Logging
{
    internal class ChunkUpsertConflictScope : IDisposable
    {
        private static readonly AsyncLocal<ChunkUpsertConflictScope?> Current = new();
        private readonly ChunkUpsertConflictScope? _parent;

        public ChunkUpsertConflictScope()
        {
            _parent = Current.Value;
            Current.Value = this;
        }

        public static bool IsActive => Current.Value is not null;

        public static bool IsConflict(Exception? exception)
        {
            PostgresException? postgres = exception switch
            {
                PostgresException error => error,
                DbUpdateException { InnerException: PostgresException error } => error,
                _ => null,
            };
            return postgres is
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                TableName: "chunks" or "chunk_ownerships",
            };
        }

        public void Dispose()
        {
            Current.Value = _parent;
            GC.SuppressFinalize(this);
        }
    }
}
