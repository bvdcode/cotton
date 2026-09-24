// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Nodes;
using Cotton.Topology;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeAncestorsQuery(string Token, Guid NodeId)
        : IRequest<GetSharedNodeAncestorsResult>;

    public class GetSharedNodeAncestorsQueryHandler(
        IMediator _mediator)
        : IRequestHandler<GetSharedNodeAncestorsQuery, GetSharedNodeAncestorsResult>
    {
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

            IReadOnlyList<Node>? ancestry = await _mediator.Send(
                new ResolveSharedNodeAncestryQuery(request.NodeId, access.NodeId, access.CreatedByUserId), ct);
            if (ancestry is null)
            {
                return new GetSharedNodeAncestorsResult(GetSharedNodeAncestorsStatus.FolderNotFound);
            }
            if (ancestry.Count - 1 > NodeHierarchy.DefaultMaxDepth)
            {
                return InvalidHierarchy("Maximum node hierarchy depth exceeded.");
            }
            List<NodeDto> ancestors = ancestry.Skip(1).Reverse().Select(node => node.Adapt<NodeDto>()).ToList();

            return new GetSharedNodeAncestorsResult(
                GetSharedNodeAncestorsStatus.Success,
                ancestors);
        }

        private static GetSharedNodeAncestorsResult InvalidHierarchy(
            string error) =>
            new(GetSharedNodeAncestorsStatus.InvalidHierarchy, Error: error);
    }
}
