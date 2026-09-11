// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Extensions
{
    internal static class ChunkGarbageCollectionQueryExtensions
    {
        public static Task<int> ScheduleGarbageCollectionAsync(
            this IQueryable<Chunk> query,
            DateTime deleteAfter,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            return query
                .Where(chunk => chunk.GCScheduledAfter == null)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(chunk => chunk.GCScheduledAfter, deleteAfter),
                    cancellationToken);
        }

        public static Task<int> CancelGarbageCollectionAsync(
            this IQueryable<Chunk> query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            return query
                .Where(chunk => chunk.GCScheduledAfter != null)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(chunk => chunk.GCScheduledAfter, (DateTime?)null),
                    cancellationToken);
        }
    }
}
