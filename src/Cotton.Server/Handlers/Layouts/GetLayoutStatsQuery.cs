// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Gets aggregate statistics for an owned layout.
    /// </summary>
    public record GetLayoutStatsQuery(
        Guid UserId,
        Guid LayoutId) : IRequest<LayoutStatsDto?>;

    /// <summary>
    /// Handles layout statistics queries.
    /// </summary>
    public class GetLayoutStatsQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetLayoutStatsQuery, LayoutStatsDto?>
    {
        /// <inheritdoc />
        public async Task<LayoutStatsDto?> Handle(
            GetLayoutStatsQuery request,
            CancellationToken ct)
        {
            Layout? layout = await _dbContext.UserLayouts
                .AsNoTracking()
                .Where(x => x.Id == request.LayoutId
                    && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct);
            if (layout is null)
            {
                return null;
            }

            int nodeCount = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.LayoutId == layout.Id
                    && x.OwnerId == request.UserId)
                .CountAsync(ct);
            int fileCount = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Node.LayoutId == layout.Id
                    && x.Node.OwnerId == request.UserId)
                .CountAsync(ct);
            long sizeBytes = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Node.LayoutId == layout.Id
                    && x.Node.OwnerId == request.UserId)
                .SumAsync(x => (long?)x.FileManifest.SizeBytes, ct) ?? 0L;

            return new LayoutStatsDto
            {
                SizeBytes = sizeBytes,
                LayoutId = layout.Id,
                NodeCount = nodeCount,
                FileCount = fileCount,
            };
        }
    }
}
