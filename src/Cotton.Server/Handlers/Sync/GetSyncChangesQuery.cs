// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Sync
{
    /// <summary>Queries the durable synchronization change feed for one user.</summary>
    public sealed class GetSyncChangesQuery(Guid userId, long sinceCursor, int limit) : IRequest<SyncChangesResponseDto>
    {
        /// <summary>Owning user identifier.</summary>
        public Guid UserId { get; } = userId;

        /// <summary>Exclusive cursor lower bound.</summary>
        public long SinceCursor { get; } = sinceCursor;

        /// <summary>Maximum number of changes to return.</summary>
        public int Limit { get; } = limit;
    }

    /// <summary>Handles durable synchronization change-feed queries.</summary>
    public sealed class GetSyncChangesQueryHandler(
        CottonDbContext _dbContext,
        SyncChangeRetentionService _retention)
        : IRequestHandler<GetSyncChangesQuery, SyncChangesResponseDto>
    {
        private const int DefaultLimit = 500;
        private const int MaximumLimit = 1000;

        /// <inheritdoc />
        public async Task<SyncChangesResponseDto> Handle(GetSyncChangesQuery request, CancellationToken ct)
        {
            await _retention.DeleteExpiredChangesAsync(request.UserId, ct);

            long? earliestAvailableCursor = await GetEarliestAvailableCursorAsync(request.UserId, ct);
            if (IsExpired(request.SinceCursor, earliestAvailableCursor))
            {
                return new SyncChangesResponseDto
                {
                    SinceCursor = request.SinceCursor,
                    NextCursor = request.SinceCursor,
                    CursorExpired = true,
                    EarliestAvailableCursor = earliestAvailableCursor,
                };
            }

            int limit = NormalizeLimit(request.Limit);
            List<SyncChange> rows = await _dbContext.SyncChanges
                .AsNoTracking()
                .Where(x => x.OwnerId == request.UserId && x.Revision > request.SinceCursor)
                .OrderBy(x => x.Revision)
                .Take(limit + 1)
                .ToListAsync(ct);

            bool hasMore = rows.Count > limit;
            if (hasMore)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            long nextCursor = rows.Count > 0
                ? rows[^1].Revision
                : request.SinceCursor;

            return new SyncChangesResponseDto
            {
                SinceCursor = request.SinceCursor,
                NextCursor = nextCursor,
                HasMore = hasMore,
                EarliestAvailableCursor = earliestAvailableCursor,
                Changes = [.. rows.Select(Map)],
            };
        }

        private async Task<long?> GetEarliestAvailableCursorAsync(Guid userId, CancellationToken ct)
        {
            long? earliestRevision = await _dbContext.SyncChanges
                .AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .OrderBy(x => x.Revision)
                .Select(x => (long?)x.Revision)
                .FirstOrDefaultAsync(ct);

            return earliestRevision.HasValue
                ? Math.Max(0, earliestRevision.Value - 1)
                : null;
        }

        private static bool IsExpired(long sinceCursor, long? earliestAvailableCursor)
        {
            return earliestAvailableCursor.HasValue
                && sinceCursor < earliestAvailableCursor.Value;
        }

        private static int NormalizeLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultLimit;
            }

            return Math.Min(limit, MaximumLimit);
        }

        private static SyncChangeDto Map(SyncChange row)
        {
            return new SyncChangeDto
            {
                Cursor = row.Revision,
                Kind = MapKind(row.Kind),
                LayoutId = row.LayoutId,
                NodeId = row.NodeId,
                NodeFileId = row.NodeFileId,
                ParentNodeId = row.ParentNodeId,
                PreviousParentNodeId = row.PreviousParentNodeId,
                FileManifestId = row.FileManifestId,
                OriginalNodeFileId = row.OriginalNodeFileId,
                Name = row.Name,
                ContentHash = row.ContentHash,
                ETag = row.ETag,
                SizeBytes = row.SizeBytes,
                CreatedAt = row.CreatedAt,
            };
        }

        private static SyncChangeKindDto MapKind(SyncChangeKind kind)
        {
            return kind switch
            {
                SyncChangeKind.FileCreated => SyncChangeKindDto.FileCreated,
                SyncChangeKind.FileContentUpdated => SyncChangeKindDto.FileContentUpdated,
                SyncChangeKind.FileRenamed => SyncChangeKindDto.FileRenamed,
                SyncChangeKind.FileMoved => SyncChangeKindDto.FileMoved,
                SyncChangeKind.FileDeleted => SyncChangeKindDto.FileDeleted,
                SyncChangeKind.FileRestored => SyncChangeKindDto.FileRestored,
                SyncChangeKind.FolderCreated => SyncChangeKindDto.FolderCreated,
                SyncChangeKind.FolderRenamed => SyncChangeKindDto.FolderRenamed,
                SyncChangeKind.FolderMoved => SyncChangeKindDto.FolderMoved,
                SyncChangeKind.FolderDeleted => SyncChangeKindDto.FolderDeleted,
                SyncChangeKind.FolderRestored => SyncChangeKindDto.FolderRestored,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sync change kind."),
            };
        }
    }
}
