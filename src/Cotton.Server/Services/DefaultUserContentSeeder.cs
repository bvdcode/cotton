// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Providers;
using Cotton.Topology.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

/// <summary>
/// Represents default user content seeder.
/// </summary>
public class DefaultUserContentSeeder(
    CottonDbContext _dbContext,
    SettingsProvider _settings,
    ILayoutService _layouts,
    ILogger<DefaultUserContentSeeder> _logger)
{
    /// <summary>
    /// Seeds default onboarding content into a newly created user account.
    /// </summary>
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

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId, ct);
            var targetRoot = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, NodeType.Default, ct);
            await CopyNodeContentsAsync(templateNodeId.Value, targetRoot, layout.Id, userId, ct);

            await transaction.CommitAsync(ct);
        });
    }

    private async Task CopyNodeContentsAsync(
        Guid sourceNodeId,
        Node targetParentNode,
        Guid targetLayoutId,
        Guid targetUserId,
        CancellationToken ct)
    {
        await CopyFilesAsync(sourceNodeId, targetParentNode.Id, targetUserId, ct);

        var sourceChildren = await _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.ParentId == sourceNodeId && x.Type == NodeType.Default)
            .OrderByDescending(x => x.NameKey)
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
                Type = NodeType.Default,
                Metadata = CopyMetadata(sourceChild.Metadata),
            };
            targetChild.SetParent(targetParentNode);
            targetChild.SetName(sourceChild.Name);

            await _dbContext.Nodes.AddAsync(targetChild, ct);
            await _dbContext.SaveChangesAsync(ct);

            await CopyNodeContentsAsync(sourceChild.Id, targetChild, targetLayoutId, targetUserId, ct);
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
            .OrderByDescending(x => x.NameKey)
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
