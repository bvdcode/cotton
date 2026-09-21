// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;

namespace Cotton.Topology
{
    public class NodeDirectory(CottonDbContext database, Node parent)
    {
        private IQueryable<Node> ScopedNodes => database.Nodes.Where(node => node.OwnerId == parent.OwnerId
            && node.LayoutId == parent.LayoutId && node.Type == parent.Type);

        private IQueryable<NodeFile> ScopedFiles => database.NodeFiles.Where(file => file.OwnerId == parent.OwnerId);

        public IQueryable<Node> Nodes => ScopedNodes.Where(node => node.ParentId == parent.Id);

        public IQueryable<NodeFile> Files => ScopedFiles.Where(file => file.NodeId == parent.Id);

        public IQueryable<Node> GetNodes(IReadOnlyCollection<Guid> parentIds)
        {
            return ScopedNodes.ChildrenOf(parentIds);
        }

        public IQueryable<NodeFile> GetFiles(IReadOnlyCollection<Guid> parentIds)
        {
            return ScopedFiles.ChildrenOf(parentIds);
        }
    }
}
