// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Topology;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public record ResolveSharedNodeAncestryQuery(Guid NodeId, Guid SharedRootNodeId, Guid OwnerId)
        : IRequest<IReadOnlyList<Node>?>;

    public class ResolveSharedNodeAncestryQueryHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
        ILogger<ResolveSharedNodeAncestryQueryHandler> _logger)
        : IRequestHandler<ResolveSharedNodeAncestryQuery, IReadOnlyList<Node>?>
    {
        private const int MaxDepth = 512;

        public async Task<IReadOnlyList<Node>?> Handle(ResolveSharedNodeAncestryQuery request, CancellationToken ct)
        {
            IQueryable<Node> nodes = _dbContext.Nodes.Where(node => node.OwnerId == request.OwnerId
                && node.Type == NodeType.Default);
            List<Node> ancestry = [];
            try
            {
                await foreach (Node node in NodeHierarchy.ReadAncestorsAsync(
                    nodes, request.NodeId, request.SharedRootNodeId, MaxDepth, ct))
                {
                    _integrity.RequireValid(_dbContext, node, ancestry.Count == 0
                        ? "shared-folder.subtree.node"
                        : "shared-folder.subtree.ancestor");
                    ancestry.Add(node);
                    if (node.Id == request.SharedRootNodeId)
                    {
                        return ancestry;
                    }
                }
            }
            catch (DatabaseIntegrityException)
            {
                return null;
            }
            catch (NodeHierarchyException exception)
            {
                _logger.LogWarning(exception, "Cannot resolve shared ancestry of node {NodeId}", request.NodeId);
            }
            return null;
        }
    }
}
