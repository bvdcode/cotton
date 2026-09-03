// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Topology.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record ResolveOwnedNodesQuery(
        Guid UserId,
        IReadOnlyCollection<Guid> NodeIds) : IRequest<IReadOnlyList<NodeDto>>
    {
        public const int MaximumNodeIds = 128;
    }

    public class ResolveOwnedNodesQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts)
        : IRequestHandler<ResolveOwnedNodesQuery, IReadOnlyList<NodeDto>>
    {
        public async Task<IReadOnlyList<NodeDto>> Handle(
            ResolveOwnedNodesQuery request,
            CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                request.NodeIds.Count,
                ResolveOwnedNodesQuery.MaximumNodeIds,
                nameof(request.NodeIds));

            Guid[] nodeIds = request.NodeIds
                .Where(nodeId => nodeId != Guid.Empty)
                .Distinct()
                .ToArray();
            if (nodeIds.Length == 0)
            {
                return [];
            }

            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(
                request.UserId,
                ct);
            List<NodeDto> nodes = await _dbContext.Nodes
                .AsNoTracking()
                .Where(node => nodeIds.Contains(node.Id)
                    && node.OwnerId == request.UserId
                    && node.LayoutId == layout.Id
                    && node.Type == NodeType.Default)
                .ProjectToType<NodeDto>()
                .ToListAsync(ct);
            Dictionary<Guid, NodeDto> nodesById = nodes.ToDictionary(node => node.Id);

            return nodeIds
                .Where(nodesById.ContainsKey)
                .Select(nodeId => nodesById[nodeId])
                .ToArray();
        }
    }
}
