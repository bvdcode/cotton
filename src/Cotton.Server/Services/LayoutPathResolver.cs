// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Resolves layout path.
    /// </summary>
    public class LayoutPathResolver(
        CottonDbContext _dbContext,
        ILayoutService _layouts)
        : ILayoutPathResolver
    {
        /// <summary>
        /// Gets layout and root.
        /// </summary>
        public async Task<(Layout Layout, Node Root)> GetLayoutAndRootAsync(Guid userId, NodeType nodeType, CancellationToken ct = default)
        {
            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId, ct);
            Node root = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, nodeType, ct);
            return (layout, root);
        }

        /// <summary>
        /// Resolves node by path async.
        /// </summary>
        public async Task<Node?> ResolveNodeByPathAsync(Guid userId, string? path, NodeType nodeType, CancellationToken ct = default)
        {
            var (layout, currentNode) = await GetLayoutAndRootAsync(userId, nodeType, ct);
            if (string.IsNullOrWhiteSpace(path))
            {
                return currentNode;
            }

            var parts = path.Replace('\\', Constants.DefaultPathSeparator).Split(Constants.DefaultPathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var nameKey = NameValidator.NormalizeAndGetNameKey(part);
                Node? nextNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.LayoutId == layout.Id
                        && x.ParentId == currentNode.Id
                        && x.OwnerId == userId
                        && x.NameKey == nameKey
                        && x.Type == nodeType)
                    .SingleOrDefaultAsync(ct);

                if (nextNode is null)
                {
                    return null;
                }

                currentNode = nextNode;
            }

            return currentNode;
        }
    }
}
