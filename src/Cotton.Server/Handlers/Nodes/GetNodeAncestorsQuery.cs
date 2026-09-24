// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Topology;
using Cotton.Topology.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record GetNodeAncestorsQuery(
        Guid UserId,
        Guid NodeId,
        NodeType NodeType) : IRequest<GetNodeAncestorsResult>;

    public class GetNodeAncestorsQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ILogger<GetNodeAncestorsQueryHandler> _logger)
        : IRequestHandler<GetNodeAncestorsQuery, GetNodeAncestorsResult>
    {
        public async Task<GetNodeAncestorsResult> Handle(
            GetNodeAncestorsQuery request,
            CancellationToken ct)
        {
            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(
                request.UserId,
                ct);
            IQueryable<Node> nodes = _dbContext.Nodes
                .AsNoTracking()
                .AccessibleTo(request.UserId)
                .Where(x => x.LayoutId == layout.Id
                    && x.Type == request.NodeType);

            bool found = false;
            List<NodeDto> ancestors = [];
            try
            {
                await foreach (Node node in NodeHierarchy.ReadAncestorsAsync(nodes, request.NodeId, cancellationToken: ct))
                {
                    if (found)
                    {
                        ancestors.Add(node.Adapt<NodeDto>());
                    }
                    found = true;
                }
            }
            catch (NodeHierarchyException exception)
            {
                _logger.LogWarning(exception, "Cannot read ancestors of node {NodeId}", request.NodeId);
                return InvalidHierarchy(exception.Message);
            }
            if (!found)
            {
                return new GetNodeAncestorsResult(GetNodeAncestorsStatus.NodeNotFound);
            }

            ancestors.Reverse();
            return new GetNodeAncestorsResult(
                GetNodeAncestorsStatus.Success,
                ancestors);
        }

        private static GetNodeAncestorsResult InvalidHierarchy(string error) =>
            new(GetNodeAncestorsStatus.InvalidHierarchy, Error: error);
    }
}
