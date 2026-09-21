// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Topology;
using Cotton.Database.Models;
using Cotton.Nodes;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record GetOwnedNodeQuery(
        Guid UserId,
        Guid NodeId) : IRequest<NodeDto?>;

    public class GetOwnedNodeQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetOwnedNodeQuery, NodeDto?>
    {
        public async Task<NodeDto?> Handle(
            GetOwnedNodeQuery request,
            CancellationToken ct)
        {
            Node? node = await _dbContext.Nodes.AccessibleTo(request.UserId)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.NodeId, ct);
            return node?.Adapt<NodeDto>();
        }
    }
}
