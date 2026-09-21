// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cotton.Topology
{
    public class LayoutNavigator(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ILogger<LayoutNavigator> _logger) : ILayoutNavigator
    {
        public async Task<(Layout Layout, Node Root)> GetLayoutAndRootAsync(Guid userId, NodeType nodeType, CancellationToken ct = default)
        {
            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId, ct);
            Node root = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, nodeType, ct);
            return (layout, root);
        }

        public async Task<Node?> ResolveNodeByPathAsync(Guid userId, string? path, NodeType nodeType, CancellationToken ct = default)
        {
            ResolvedNodePath? resolved = await ResolveNodePathAsync(userId, path, nodeType, ct);
            return resolved?.Node;
        }

        public async Task<ResolvedNodePath?> ResolveNodePathAsync(Guid userId, string? path, NodeType nodeType, CancellationToken ct = default)
        {
            var (_, currentNode) = await GetLayoutAndRootAsync(userId, nodeType, ct);
            if (string.IsNullOrWhiteSpace(path))
            {
                return new(currentNode, string.Empty);
            }

            string[] parts = (path ?? string.Empty)
                .Replace('\\', Constants.DefaultPathSeparator)
                .Trim(Constants.DefaultPathSeparator)
                .Split(Constants.DefaultPathSeparator, StringSplitOptions.RemoveEmptyEntries);
            List<string> resolvedParts = new(parts.Length);
            foreach (string part in parts)
            {
                Node? nextNode = await FindChildNodeAsync(currentNode, part, ct);

                if (nextNode is null)
                {
                    return null;
                }

                currentNode = nextNode;
                resolvedParts.Add(nextNode.Name);
            }

            return new(currentNode, string.Join(Constants.DefaultPathSeparator, resolvedParts));
        }

        public Task<Node?> FindChildNodeAsync(Node parent, string name, CancellationToken ct = default)
        {
            string nameKey = NameValidator.NormalizeAndGetNameKey(name);
            return new NodeDirectory(_dbContext, parent).Nodes.AsNoTracking()
                .SingleOrDefaultAsync(node => node.NameKey == nameKey, ct);
        }

        public async Task<string?> GetNodePathFromRootAsync(Guid userId, Guid nodeId, NodeType nodeType, CancellationToken ct = default)
        {
            IQueryable<Node> nodes = _dbContext.Nodes.AsNoTracking().AccessibleTo(userId)
                .Where(node => node.Type == nodeType);
            Stack<string> parts = new();
            try
            {
                await foreach (var node in NodeHierarchy.ReadAncestorsAsync(
                    nodes, nodeId, node => new { node.ParentId, node.Name }, node => node.ParentId, cancellationToken: ct))
                {
                    if (!node.ParentId.HasValue)
                    {
                        return string.Join(Constants.DefaultPathSeparator, parts);
                    }
                    parts.Push(node.Name);
                }
            }
            catch (NodeHierarchyException exception)
            {
                _logger.LogWarning(exception, "Cannot resolve path for node {NodeId}", nodeId);
            }
            return null;
        }

        public async Task<(Node Parent, string ResourceName)?> ResolveParentAndNameAsync(Guid userId, string path, NodeType nodeType, CancellationToken ct = default)
        {
            string cleanPath = (path ?? string.Empty).Replace('\\', Constants.DefaultPathSeparator).Trim(Constants.DefaultPathSeparator);
            if (string.IsNullOrEmpty(cleanPath))
            {
                return null;
            }

            string[] parts = cleanPath.Split(Constants.DefaultPathSeparator, StringSplitOptions.RemoveEmptyEntries);
            string resourceName = parts[^1];
            string? parentPath = parts.Length == 1 ? null : string.Join(Constants.DefaultPathSeparator, parts.Take(parts.Length - 1));

            Node? parent = await ResolveNodeByPathAsync(userId, parentPath, nodeType, ct);
            return parent is null ? null : (parent, resourceName);
        }
    }
}
