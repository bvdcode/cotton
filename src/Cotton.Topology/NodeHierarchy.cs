// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Cotton.Topology
{
    public static class NodeHierarchy
    {
        public const int DefaultMaxDepth = 256;

        public static IAsyncEnumerable<Node> ReadAncestorsAsync(
            IQueryable<Node> nodes,
            Guid nodeId,
            Guid? stopAtId = null,
            int maxDepth = DefaultMaxDepth,
            CancellationToken cancellationToken = default)
        {
            return ReadAncestorsAsync(nodes, nodeId, node => node, node => node.ParentId,
                stopAtId, maxDepth, cancellationToken);
        }

        public static async IAsyncEnumerable<T> ReadAncestorsAsync<T>(
            IQueryable<Node> nodes,
            Guid nodeId,
            Expression<Func<Node, T>> projection,
            Func<T, Guid?> getParentId,
            Guid? stopAtId = null,
            int maxDepth = DefaultMaxDepth,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where T : class
        {
            HashSet<Guid> visited = [];
            Guid? currentId = nodeId;
            int depth = 0;
            while (currentId.HasValue)
            {
                if (depth++ > maxDepth)
                {
                    throw new NodeHierarchyException("Maximum node hierarchy depth exceeded.");
                }
                if (!visited.Add(currentId.Value))
                {
                    throw new NodeHierarchyException("Circular reference detected in node hierarchy.");
                }

                T? node = await nodes.Where(n => n.Id == currentId.Value)
                    .Select(projection).SingleOrDefaultAsync(cancellationToken);
                if (node is null)
                {
                    yield break;
                }

                yield return node;
                if (currentId == stopAtId)
                {
                    yield break;
                }
                currentId = getParentId(node);
            }
        }

        public static async Task<Dictionary<Guid, string>> ResolvePathsAsync(
            IQueryable<Node> nodes,
            IReadOnlyCollection<Guid> nodeIds,
            CancellationToken cancellationToken = default)
        {
            Dictionary<Guid, (Guid? ParentId, string Name, NodeType Type)> lineage = [];
            HashSet<Guid> frontier = new(nodeIds);
            while (frontier.Count > 0)
            {
                Guid[] ids = [.. frontier];
                frontier.Clear();
                var level = await nodes.AsNoTracking()
                    .Where(node => ids.Contains(node.Id))
                    .Select(node => new { node.Id, node.ParentId, node.Name, node.Type })
                    .ToListAsync(cancellationToken);
                foreach (var node in level)
                {
                    if (!lineage.TryAdd(node.Id, (node.ParentId, node.Name, node.Type)))
                    {
                        continue;
                    }
                    if (node.ParentId.HasValue && !lineage.ContainsKey(node.ParentId.Value))
                    {
                        frontier.Add(node.ParentId.Value);
                    }
                }
                frontier.RemoveWhere(lineage.ContainsKey);
            }

            Dictionary<Guid, string> paths = new(nodeIds.Count);
            foreach (Guid id in nodeIds)
            {
                Stack<string> parts = new();
                HashSet<Guid> visited = [];
                Guid currentId = id;
                while (lineage.TryGetValue(currentId, out (Guid? ParentId, string Name, NodeType Type) node))
                {
                    if (!visited.Add(currentId) || parts.Count >= DefaultMaxDepth)
                    {
                        break;
                    }
                    parts.Push(node.Name);
                    if (!node.ParentId.HasValue
                        || (lineage.TryGetValue(node.ParentId.Value, out (Guid? ParentId, string Name, NodeType Type) parent) && parent.Type != node.Type))
                    {
                        break;
                    }
                    currentId = node.ParentId.Value;
                }
                paths[id] = Constants.DefaultPathSeparator + string.Join(Constants.DefaultPathSeparator, parts);
            }
            return paths;
        }
    }
}
