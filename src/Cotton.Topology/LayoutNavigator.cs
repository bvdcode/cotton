// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Shared;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Topology;

public sealed class LayoutNavigator(
    CottonDbContext _dbContext,
    ILayoutService _layouts) : ILayoutNavigator
{
    public async Task<(Layout Layout, Node Root)> GetLayoutAndRootAsync(Guid userId, NodeType nodeType, CancellationToken ct = default)
    {
        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
        var root = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, nodeType);
        return (layout, root);
    }

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
            var nextNode = await _dbContext.Nodes
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

        var parent = await ResolveNodeByPathAsync(userId, parentPath, nodeType, ct);
        return parent is null ? null : (parent, resourceName);
    }
}
