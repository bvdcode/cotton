// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Topology;

/// <summary>
/// EF-backed implementation of path resolution for typed Cotton layout trees.
/// </summary>
public class LayoutNavigator(
    CottonDbContext _dbContext,
    ILayoutService _layouts) : ILayoutNavigator
{
    /// <inheritdoc />
    public async Task<(Layout Layout, Node Root)> GetLayoutAndRootAsync(Guid userId, NodeType nodeType, CancellationToken ct = default)
    {
        Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId, ct);
        Node root = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, nodeType, ct);
        return (layout, root);
    }

    /// <inheritdoc />
    public async Task<Node?> ResolveNodeByPathAsync(Guid userId, string? path, NodeType nodeType, CancellationToken ct = default)
    {
        var (layout, currentNode) = await GetLayoutAndRootAsync(userId, nodeType, ct);
        if (string.IsNullOrWhiteSpace(path))
        {
            return currentNode;
        }

        var parts = (path ?? string.Empty)
            .Replace('\\', Constants.DefaultPathSeparator)
            .Trim(Constants.DefaultPathSeparator)
            .Split(Constants.DefaultPathSeparator, StringSplitOptions.RemoveEmptyEntries);
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

    /// <inheritdoc />
    public async Task<string?> GetNodePathFromRootAsync(Guid userId, Guid nodeId, NodeType nodeType, CancellationToken ct = default)
    {
        const int maxDepth = 256;

        var current = await _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.Id == nodeId && x.OwnerId == userId && x.Type == nodeType)
            .Select(x => new { x.Id, x.ParentId, x.Name })
            .SingleOrDefaultAsync(ct);
        if (current is null)
        {
            return null;
        }

        var parts = new Stack<string>();
        var visited = new HashSet<Guid>();
        var depth = 0;

        while (current.ParentId.HasValue)
        {
            if (!visited.Add(current.Id) || depth++ >= maxDepth)
            {
                return null;
            }

            parts.Push(current.Name);
            current = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == current.ParentId.Value && x.OwnerId == userId && x.Type == nodeType)
                .Select(x => new { x.Id, x.ParentId, x.Name })
                .SingleOrDefaultAsync(ct);
            if (current is null)
            {
                return null;
            }
        }

        return string.Join(Constants.DefaultPathSeparator, parts);
    }

    /// <inheritdoc />
    public async Task<(Node Parent, string ResourceName)?> ResolveParentAndNameAsync(Guid userId, string path, NodeType nodeType, CancellationToken ct = default)
    {
        var cleanPath = (path ?? string.Empty).Replace('\\', Constants.DefaultPathSeparator).Trim(Constants.DefaultPathSeparator);
        if (string.IsNullOrEmpty(cleanPath))
        {
            return null;
        }

        var parts = cleanPath.Split(Constants.DefaultPathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var resourceName = parts[^1];
        var parentPath = parts.Length == 1 ? null : string.Join(Constants.DefaultPathSeparator, parts.Take(parts.Length - 1));

        Node? parent = await ResolveNodeByPathAsync(userId, parentPath, nodeType, ct);
        return parent is null ? null : (parent, resourceName);
    }
}
