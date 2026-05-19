// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Providers;
using Cotton.Topology.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public class DefaultUserContentSeeder(
    CottonDbContext _dbContext,
    SettingsProvider _settings,
    ILayoutService _layouts,
    ILogger<DefaultUserContentSeeder> _logger)
{
    public async Task SeedAsync(Guid userId, CancellationToken ct = default)
    {
        Guid? templateNodeId = _settings.GetServerSettings().DefaultUserTemplateNodeId;
        if (templateNodeId is null || templateNodeId == Guid.Empty)
        {
            return;
        }

        bool templateExists = await _dbContext.Nodes
            .AsNoTracking()
            .AnyAsync(x => x.Id == templateNodeId.Value && x.Type == NodeType.Default, ct);
        if (!templateExists)
        {
            _logger.LogWarning(
                "Default user template node {TemplateNodeId} is not available; skipping seed for user {UserId}.",
                templateNodeId,
                userId);
            return;
        }

        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
        var targetRoot = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, NodeType.Default);
        await CopyNodeContentsAsync(templateNodeId.Value, targetRoot.Id, layout.Id, userId, ct);
    }

    private async Task CopyNodeContentsAsync(
        Guid sourceNodeId,
        Guid targetParentNodeId,
        Guid targetLayoutId,
        Guid targetUserId,
        CancellationToken ct)
    {
        await CopyFilesAsync(sourceNodeId, targetParentNodeId, targetUserId, ct);

        var sourceChildren = await _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.ParentId == sourceNodeId && x.Type == NodeType.Default)
            .OrderBy(x => x.NameKey)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Metadata,
            })
            .ToListAsync(ct);

        foreach (var sourceChild in sourceChildren)
        {
            var targetChild = new Node
            {
                OwnerId = targetUserId,
                LayoutId = targetLayoutId,
                ParentId = targetParentNodeId,
                Type = NodeType.Default,
                Metadata = CopyMetadata(sourceChild.Metadata),
            };
            targetChild.SetName(sourceChild.Name);

            await _dbContext.Nodes.AddAsync(targetChild, ct);
            await _dbContext.SaveChangesAsync(ct);

            await CopyNodeContentsAsync(sourceChild.Id, targetChild.Id, targetLayoutId, targetUserId, ct);
        }
    }

    private async Task CopyFilesAsync(
        Guid sourceNodeId,
        Guid targetParentNodeId,
        Guid targetUserId,
        CancellationToken ct)
    {
        var sourceFiles = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.NodeId == sourceNodeId)
            .OrderBy(x => x.NameKey)
            .Select(x => new
            {
                x.FileManifestId,
                x.Name,
                x.Metadata,
            })
            .ToListAsync(ct);

        foreach (var sourceFile in sourceFiles)
        {
            var targetFile = new NodeFile
            {
                OwnerId = targetUserId,
                NodeId = targetParentNodeId,
                FileManifestId = sourceFile.FileManifestId,
                Metadata = CopyMetadata(sourceFile.Metadata),
            };
            targetFile.SetName(sourceFile.Name);

            await _dbContext.NodeFiles.AddAsync(targetFile, ct);
            await _dbContext.SaveChangesAsync(ct);

            targetFile.OriginalNodeFileId = targetFile.Id;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private static Dictionary<string, string> CopyMetadata(Dictionary<string, string>? metadata)
    {
        return metadata is { Count: > 0 }
            ? new Dictionary<string, string>(metadata)
            : [];
    }
}
