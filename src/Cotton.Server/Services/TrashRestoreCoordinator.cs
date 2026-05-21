// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Models.Dto;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public class TrashRestoreCoordinator(
    CottonDbContext _dbContext,
    ILayoutService _layouts,
    ILayoutNavigator _navigator,
    IMediator _mediator)
{
    public readonly record struct ConflictInfo(RestoreConflictKind Kind, string Name, Guid Id);
    public readonly record struct ParentResolution(Node? Parent, string? InvalidPathReason);

    public async Task<ParentResolution> ResolveOrCreateParentAsync(
        Guid userId,
        string originalParentPath,
        bool createMissingParents,
        CancellationToken ct)
    {
        Node? parent;
        try
        {
            parent = await _navigator.ResolveNodeByPathAsync(userId, originalParentPath, NodeType.Default, ct);
        }
        catch (ArgumentException ex)
        {
            return new ParentResolution(null, $"Stored original parent path is invalid: {ex.Message}");
        }

        if (parent is not null || !createMissingParents)
        {
            return new ParentResolution(parent, null);
        }

        return await CreateMissingParentsAsync(userId, originalParentPath, ct);
    }

    public async Task<ConflictInfo?> FindConflictAsync(Guid userId, Guid parentId, string nameKey, CancellationToken ct)
    {
        var folder = await _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.ParentId == parentId
                && x.OwnerId == userId
                && x.NameKey == nameKey
                && x.Type == NodeType.Default)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct);
        if (folder is not null)
        {
            return new ConflictInfo(RestoreConflictKind.Folder, folder.Name, folder.Id);
        }

        var file = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.NodeId == parentId
                && x.OwnerId == userId
                && x.NameKey == nameKey)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct);
        if (file is not null)
        {
            return new ConflictInfo(RestoreConflictKind.File, file.Name, file.Id);
        }

        return null;
    }

    public Task SendConflictToTrashAsync(Guid userId, ConflictInfo conflict, CancellationToken ct)
        => conflict.Kind == RestoreConflictKind.Folder
            ? _mediator.Send(new DeleteNodeQuery(userId, conflict.Id, skipTrash: false), ct)
            : _mediator.Send(new DeleteFileQuery(userId, conflict.Id, skipTrash: false), ct);

    public static string GetOriginalParentPath(Dictionary<string, string>? metadata)
        => metadata is not null
            && metadata.TryGetValue(TrashMetadataKeys.OriginalParentPath, out string? path)
                ? path
                : string.Empty;

    public static Dictionary<string, string> SetOriginalParentPath(
        Dictionary<string, string>? metadata,
        string originalParentPath)
    {
        var copy = metadata is null
            ? []
            : new Dictionary<string, string>(metadata);
        copy[TrashMetadataKeys.OriginalParentPath] = originalParentPath;
        return copy;
    }

    public static Dictionary<string, string> RemoveOriginalParentPath(Dictionary<string, string>? metadata)
    {
        var copy = metadata is null
            ? []
            : new Dictionary<string, string>(metadata);
        copy.Remove(TrashMetadataKeys.OriginalParentPath);
        return copy;
    }

    public async Task DeleteWrapperIfEmptyAsync(Guid userId, Node wrapper, CancellationToken ct)
    {
        bool stillHasChildren = await _dbContext.Nodes
            .AnyAsync(x => x.ParentId == wrapper.Id && x.OwnerId == userId, ct);
        if (stillHasChildren)
        {
            return;
        }

        bool stillHasFiles = await _dbContext.NodeFiles
            .AnyAsync(x => x.NodeId == wrapper.Id && x.OwnerId == userId, ct);
        if (stillHasFiles)
        {
            return;
        }

        _dbContext.Nodes.Remove(wrapper);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<ParentResolution> CreateMissingParentsAsync(Guid userId, string originalParentPath, CancellationToken ct)
    {
        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
        var (_, current) = await _navigator.GetLayoutAndRootAsync(userId, NodeType.Default, ct);

        if (string.IsNullOrWhiteSpace(originalParentPath))
        {
            return new ParentResolution(current, null);
        }

        var parts = originalParentPath
            .Replace('\\', Constants.DefaultPathSeparator)
            .Trim(Constants.DefaultPathSeparator)
            .Split(Constants.DefaultPathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string part in parts)
        {
            if (!NameValidator.TryNormalizeAndValidate(part, out string normalized, out _))
            {
                return new ParentResolution(
                    null,
                    $"Stored original parent path contains an invalid segment '{part}'.");
            }

            string nameKey = NameValidator.GetNameKey(normalized);
            var existing = await _dbContext.Nodes
                .Where(x => x.LayoutId == layout.Id
                    && x.ParentId == current.Id
                    && x.OwnerId == userId
                    && x.NameKey == nameKey
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);

            if (existing is not null)
            {
                current = existing;
                continue;
            }

            var created = new Node
            {
                OwnerId = userId,
                Type = NodeType.Default,
                LayoutId = layout.Id,
            };
            created.SetParent(current);
            created.SetName(normalized);
            await _dbContext.Nodes.AddAsync(created, ct);
            await _dbContext.SaveChangesAsync(ct);
            current = created;
        }

        return new ParentResolution(current, null);
    }
}
