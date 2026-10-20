// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Contains explicitly approved database mutations that bypass change tracking.
    /// </summary>
    /// <remarks>
    /// No method or other member may be added, and no existing operation may be changed,
    /// without two separate, explicit confirmations from the requesting user for that exact change.
    /// Every mutation must have a behavioral test proving that its updated fields are excluded
    /// from the actual integrity descriptor and that existing signatures remain valid.
    /// The source approval test must not be updated to admit a change before both confirmations.
    /// </remarks>
#pragma warning disable EEX0015
    internal static class ApprovedBulkMutationExtensions
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
    }
#pragma warning restore EEX0015
}
