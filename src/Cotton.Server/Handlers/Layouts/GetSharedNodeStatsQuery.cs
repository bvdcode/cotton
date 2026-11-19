// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Nodes;
using Cotton.Server.Handlers.Nodes;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeStatsQuery(string Token, Guid? NodeId) : IRequest<FolderStatsDto?>;

    public class GetSharedNodeStatsQueryHandler(IMediator mediator)
        : IRequestHandler<GetSharedNodeStatsQuery, FolderStatsDto?>
    {
        public async Task<FolderStatsDto?> Handle(GetSharedNodeStatsQuery request, CancellationToken ct)
        {
            SharedNodeAccess? access = await mediator.Send(new ResolveSharedNodeAccessQuery(request.Token), ct);
            if (access is null || access.HideFolderContent)
            {
                return null;
            }

            Guid nodeId = request.NodeId ?? access.NodeId;
            IReadOnlyList<Node>? ancestry = await mediator.Send(
                new ResolveSharedNodeAncestryQuery(nodeId, access.NodeId, access.CreatedByUserId), ct);
            if (ancestry is null)
            {
                return null;
            }

            return await mediator.Send(new GetFolderStatsQuery(access.CreatedByUserId, nodeId, true), ct);
        }
    }
}
