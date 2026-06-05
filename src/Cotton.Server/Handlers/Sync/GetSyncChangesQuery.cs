// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Contracts.Sync;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Sync
{
    /// <summary>
    /// Represents a durable sync-change feed query sent through the mediator pipeline.
    /// </summary>
    public class GetSyncChangesQuery(Guid userId, long sinceCursor, int limit)
        : IRequest<SyncChangesResponseDto>
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the exclusive cursor lower bound.
        /// </summary>
        public long SinceCursor { get; } = sinceCursor;
        /// <summary>
        /// Gets the maximum number of changes to return.
        /// </summary>
        public int Limit { get; } = limit;
    }

    /// <summary>
    /// Handles durable sync-change feed queries in the mediator pipeline.
    /// </summary>
    public class GetSyncChangesQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetSyncChangesQuery, SyncChangesResponseDto>
    {
        private const int MaximumLimit = 1000;

        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<SyncChangesResponseDto> Handle(GetSyncChangesQuery request, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(request.SinceCursor);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Limit);
            int limit = Math.Min(request.Limit, MaximumLimit);
            long? earliestAvailableCursor = await GetEarliestAvailableCursorAsync(request.UserId, ct);
            if (earliestAvailableCursor.HasValue && request.SinceCursor < earliestAvailableCursor.Value)
            {
                return new SyncChangesResponseDto
                {
                    SinceCursor = request.SinceCursor,
                    NextCursor = request.SinceCursor,
                    CursorExpired = true,
                    EarliestAvailableCursor = earliestAvailableCursor,
                };
            }

            List<SyncChange> rows = await _dbContext.SyncChanges
                .AsNoTracking()
                .Where(x => x.OwnerId == request.UserId && x.Id > request.SinceCursor)
                .OrderBy(x => x.Id)
                .Take(limit + 1)
                .ToListAsync(ct);

            bool hasMore = rows.Count > limit;
            if (hasMore)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            long nextCursor = rows.Count > 0
                ? rows[^1].Id
                : request.SinceCursor;

            return new SyncChangesResponseDto
            {
                SinceCursor = request.SinceCursor,
                NextCursor = nextCursor,
                HasMore = hasMore,
                EarliestAvailableCursor = earliestAvailableCursor,
                Changes = rows.Adapt<List<SyncChangeDto>>(),
            };
        }

        private async Task<long?> GetEarliestAvailableCursorAsync(Guid userId, CancellationToken ct)
        {
            long? earliestId = await _dbContext.SyncChanges
                .AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .OrderBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(ct);

            return earliestId.HasValue
                ? Math.Max(0, earliestId.Value - 1)
                : null;
        }
    }
}
