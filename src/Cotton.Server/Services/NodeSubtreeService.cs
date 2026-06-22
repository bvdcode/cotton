// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates node subtree.
    /// </summary>
    public class NodeSubtreeService(CottonDbContext _dbContext)
    {
        /// <summary>
        /// Collects subtree ids.
        /// </summary>
        public async Task<HashSet<Guid>> CollectSubtreeIdsAsync(Guid userId, Guid rootId, CancellationToken ct)
        {
            var visited = new HashSet<Guid> { rootId };
            var frontier = new List<Guid> { rootId };

            while (frontier.Count > 0)
            {
                var batch = frontier.ToArray();
                frontier.Clear();

                var children = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.OwnerId == userId
                        && x.ParentId != null
                        && batch.Contains(x.ParentId.Value))
                    .Select(x => x.Id)
                    .ToListAsync(ct);

                foreach (var childId in children)
                {
                    if (visited.Add(childId))
                    {
                        frontier.Add(childId);
                    }
                }
            }

            return visited;
        }

        /// <summary>
        /// Sets subtree type async.
        /// </summary>
        public async Task SetSubtreeTypeAsync(Guid userId, Guid rootId, NodeType newType, CancellationToken ct)
        {
            var ids = (await CollectSubtreeIdsAsync(userId, rootId, ct)).ToArray();
            List<Node> nodes = await _dbContext.Nodes
                .Where(x => x.OwnerId == userId && ids.Contains(x.Id))
                .ToListAsync(ct);

            foreach (Node node in nodes)
            {
                node.Type = newType;
            }
        }
    }
}
