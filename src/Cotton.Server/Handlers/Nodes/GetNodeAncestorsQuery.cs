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
    public record GetNodeAncestorsQuery(
        Guid UserId,
        Guid NodeId,
        NodeType NodeType) : IRequest<GetNodeAncestorsResult>;

    public class GetNodeAncestorsQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts)
        : IRequestHandler<GetNodeAncestorsQuery, GetNodeAncestorsResult>
    {
        private const int MaxDepth = 256;

        public async Task<GetNodeAncestorsResult> Handle(
            GetNodeAncestorsQuery request,
            CancellationToken ct)
        {
            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(
                request.UserId,
                ct);
            IQueryable<Node> nodes = _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == request.NodeType);

            Node? currentNode = await nodes.SingleOrDefaultAsync(
                x => x.Id == request.NodeId,
                ct);
            if (currentNode is null)
            {
                return new GetNodeAncestorsResult(
                    GetNodeAncestorsStatus.NodeNotFound);
            }

            HashSet<Guid> visited = [currentNode.Id];
            List<NodeDto> ancestors = [];
            int depth = 0;
            while (currentNode.ParentId.HasValue)
            {
                if (depth++ >= MaxDepth)
                {
                    return InvalidHierarchy(
                        "Maximum node hierarchy depth exceeded.");
                }

                Guid parentId = currentNode.ParentId.Value;
                if (!visited.Add(parentId))
                {
                    return InvalidHierarchy(
                        "Circular reference detected in node hierarchy.");
                }

                Node? parentNode = await nodes.SingleOrDefaultAsync(
                    x => x.Id == parentId,
                    ct);
                if (parentNode is null)
                {
                    break;
                }

                ancestors.Add(parentNode.Adapt<NodeDto>());
                currentNode = parentNode;
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
