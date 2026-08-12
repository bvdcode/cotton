// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeAncestorsQuery(string Token, Guid NodeId)
        : IRequest<GetSharedNodeAncestorsResult>;

    public class GetSharedNodeAncestorsQueryHandler(
        IMediator _mediator,
        CottonDbContext _dbContext)
        : IRequestHandler<GetSharedNodeAncestorsQuery, GetSharedNodeAncestorsResult>
    {
        private const int MaxDepth = 256;

        public async Task<GetSharedNodeAncestorsResult> Handle(
            GetSharedNodeAncestorsQuery request,
            CancellationToken ct)
        {
            SharedNodeAccess? access = await _mediator.Send(
                new ResolveSharedNodeAccessQuery(request.Token),
                ct);
            if (access is null)
            {
                return new GetSharedNodeAncestorsResult(
                    GetSharedNodeAncestorsStatus.SharedFolderNotFound);
            }

            bool canAccessNode = await _mediator.Send(
                new VerifySharedNodeSubtreeAccessQuery(
                    request.NodeId,
                    access.NodeId,
                    access.CreatedByUserId),
                ct);
            if (!canAccessNode)
            {
                return new GetSharedNodeAncestorsResult(
                    GetSharedNodeAncestorsStatus.FolderNotFound);
            }

            Node? currentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == access.CreatedByUserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);
            if (currentNode is null)
            {
                return new GetSharedNodeAncestorsResult(
                    GetSharedNodeAncestorsStatus.FolderNotFound);
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

                Node? parentNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == parentId
                        && x.OwnerId == access.CreatedByUserId
                        && x.Type == NodeType.Default)
                    .SingleOrDefaultAsync(ct);
                if (parentNode is null)
                {
                    break;
                }

                ancestors.Add(parentNode.Adapt<NodeDto>());
                if (parentNode.Id == access.NodeId)
                {
                    break;
                }

                currentNode = parentNode;
            }

            ancestors.Reverse();
            return new GetSharedNodeAncestorsResult(
                GetSharedNodeAncestorsStatus.Success,
                ancestors);
        }

        private static GetSharedNodeAncestorsResult InvalidHierarchy(
            string error) =>
            new(GetSharedNodeAncestorsStatus.InvalidHierarchy, Error: error);
    }
}
