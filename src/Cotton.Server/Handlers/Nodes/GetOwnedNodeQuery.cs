// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Nodes;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Gets an owned node by identifier.
    /// </summary>
    public record GetOwnedNodeQuery(
        Guid UserId,
        Guid NodeId) : IRequest<NodeDto?>;

    /// <summary>
    /// Handles owned node queries.
    /// </summary>
    public class GetOwnedNodeQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetOwnedNodeQuery, NodeDto?>
    {
        /// <inheritdoc />
        public async Task<NodeDto?> Handle(
            GetOwnedNodeQuery request,
            CancellationToken ct)
        {
            Node? node = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct);
            return node?.Adapt<NodeDto>();
        }
    }
}
