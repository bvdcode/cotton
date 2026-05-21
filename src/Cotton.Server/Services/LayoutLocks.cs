// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Per-layout serialization primitive for namespace and tree mutations.
    /// Backed by PostgreSQL transaction-scoped advisory locks: the lock is
    /// auto-released on COMMIT or ROLLBACK of the calling transaction.
    /// </summary>
    /// <remarks>
    /// Used by namespace writers (create, rename, move, WebDAV MKCOL/PUT/COPY/MOVE)
    /// before they validate or create a NameKey under a layout. Pure deletes usually
    /// do not need the lock because they only remove namespace entries; a delete flow
    /// that also creates or reparents entries should take the relevant layout lock.
    /// Caller is responsible for opening the surrounding transaction and committing it.
    /// </remarks>
    internal static class LayoutLocks
    {
        public static Task AcquireForLayoutAsync(
            CottonDbContext dbContext,
            Guid layoutId,
            CancellationToken ct)
        {
            long lockKey = LockKeyFor(layoutId);
            return dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})",
                ct);
        }

        private static long LockKeyFor(Guid layoutId)
        {
            // pg_advisory_xact_lock takes a bigint. Collapse the 16-byte Guid into
            // one deterministic int64 via XOR of its two halves. A spurious collision
            // just means two unrelated layouts share the same lock: performance hit
            // only, never correctness.
            var bytes = layoutId.ToByteArray();
            long high = BitConverter.ToInt64(bytes, 0);
            long low = BitConverter.ToInt64(bytes, 8);
            return high ^ low;
        }
    }
}
