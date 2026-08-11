// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Verifies that a node belongs to a shared folder subtree.
    /// </summary>
    public record VerifySharedNodeSubtreeAccessQuery(
        Guid NodeId,
        Guid SharedRootNodeId,
        Guid OwnerId) : IRequest<bool>;

    /// <summary>
    /// Handles shared subtree access verification.
    /// </summary>
    public class VerifySharedNodeSubtreeAccessQueryHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<VerifySharedNodeSubtreeAccessQuery, bool>
    {
        private const int MaxDepth = 512;

        /// <summary>
        /// Walks the verified parent chain until the shared root is reached.
        /// </summary>
        public async Task<bool> Handle(
            VerifySharedNodeSubtreeAccessQuery request,
            CancellationToken ct)
        {
            Node? currentNode = await LoadVerifiedSharedDefaultNodeAsync(
                request.NodeId,
                request.OwnerId,
                "shared-folder.subtree.node",
                ct);
            if (currentNode is null)
            {
                return false;
            }

            if (currentNode.Id == request.SharedRootNodeId)
            {
                return true;
            }

            HashSet<Guid> visited = [currentNode.Id];
            int depth = 0;

            while (currentNode.ParentId.HasValue)
            {
                if (depth++ >= MaxDepth)
                {
                    return false;
                }

                Guid parentId = currentNode.ParentId.Value;
                if (!visited.Add(parentId))
                {
                    return false;
                }

                currentNode = await LoadVerifiedSharedDefaultNodeAsync(
                    parentId,
                    request.OwnerId,
                    "shared-folder.subtree.ancestor",
                    ct);
                if (currentNode is null)
                {
                    return false;
                }

                if (currentNode.Id == request.SharedRootNodeId)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<Node?> LoadVerifiedSharedDefaultNodeAsync(
            Guid nodeId,
            Guid ownerId,
            string boundary,
            CancellationToken ct)
        {
            Node? node = await _dbContext.Nodes
                .Where(x => x.Id == nodeId
                    && x.OwnerId == ownerId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);
            if (node is null)
            {
                return null;
            }

            try
            {
                _integrity.RequireValid(_dbContext, node, boundary);
            }
            catch (DatabaseIntegrityException)
            {
                return null;
            }

            return node;
        }
    }
}
