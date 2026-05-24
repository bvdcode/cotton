// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Represents a get recent nodes query sent through the mediator pipeline.
    /// </summary>
    public class GetRecentNodesQuery(Guid userId, Guid layoutId, int count) : IRequest<IEnumerable<NodeFileManifestDto>>
    {
        /// <summary>
        /// Gets the count.
        /// </summary>
        public int Count { get; } = count;
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the layout identifier.
        /// </summary>
        public Guid LayoutId { get; } = layoutId;
    }

    /// <summary>
    /// Handles get recent nodes queries in the mediator pipeline.
    /// </summary>
    public class GetRecentNodesQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetRecentNodesQuery, IEnumerable<NodeFileManifestDto>>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<IEnumerable<NodeFileManifestDto>> Handle(GetRecentNodesQuery request, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Count);
            return await _dbContext.NodeFiles
                .AsNoTracking()
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Node.Type == NodeType.Default)
                .Where(x => x.OwnerId == request.UserId && x.Node.LayoutId == request.LayoutId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(request.Count)
                .ProjectToType<NodeFileManifestDto>()
                .ToListAsync(ct);
        }
    }
}
