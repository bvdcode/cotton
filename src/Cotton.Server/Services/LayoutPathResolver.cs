// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public class LayoutPathResolver(
    CottonDbContext _dbContext,
    ILayoutService _layouts)
    : ILayoutPathResolver
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

        var parts = path.Replace('\\', Constants.DefaultPathSeparator).Split(Constants.DefaultPathSeparator, StringSplitOptions.RemoveEmptyEntries);
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
}
