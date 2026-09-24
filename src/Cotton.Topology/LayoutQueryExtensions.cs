// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Topology
{
    public static class LayoutQueryExtensions
    {
        public static IQueryable<Node> AccessibleTo(this IQueryable<Node> nodes, Guid userId)
        {
            return nodes.Where(node => node.OwnerId == userId);
        }

        public static IQueryable<NodeFile> AccessibleTo(this IQueryable<NodeFile> files, Guid userId)
        {
            return files.Where(file => file.OwnerId == userId);
        }

        public static IQueryable<Node> ChildrenOf(this IQueryable<Node> nodes, IReadOnlyCollection<Guid> parentIds)
        {
            return nodes.Where(node => node.ParentId != null && parentIds.Contains(node.ParentId.Value));
        }

        public static IQueryable<NodeFile> ChildrenOf(this IQueryable<NodeFile> files, IReadOnlyCollection<Guid> parentIds)
        {
            return files.Where(file => parentIds.Contains(file.NodeId));
        }
    }
}
