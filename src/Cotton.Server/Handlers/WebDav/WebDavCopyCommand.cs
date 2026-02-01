// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services.WebDav;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV COPY operation
/// </summary>
public record WebDavCopyCommand(
    Guid UserId,
    string SourcePath,
    string DestinationPath,
    bool Overwrite = false) : IRequest<WebDavCopyResult>;

/// <summary>
/// Result of WebDAV COPY operation
/// </summary>
public record WebDavCopyResult(
    bool Success,
    bool Created,
    WebDavCopyError? Error = null);

public enum WebDavCopyError
{
    SourceNotFound,
    DestinationParentNotFound,
    DestinationExists,
    InvalidName,
    CannotCopyRoot
}

/// <summary>
/// Handler for WebDAV COPY operation
/// </summary>
public class WebDavCopyCommandHandler(
    CottonDbContext _dbContext,
    ILayoutService _layouts,
    IWebDavPathResolver _pathResolver,
    ILogger<WebDavCopyCommandHandler> _logger)
    : IRequestHandler<WebDavCopyCommand, WebDavCopyResult>
{
    public async Task<WebDavCopyResult> Handle(WebDavCopyCommand request, CancellationToken ct)
    {
        // Resolve source
        var sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
        if (!sourceResult.Found)
        {
            _logger.LogDebug("WebDAV COPY: Source not found: {Path}", request.SourcePath);
            return new WebDavCopyResult(false, false, WebDavCopyError.SourceNotFound);
        }

        // Can't copy root
        if (sourceResult.IsCollection && sourceResult.Node?.ParentId is null)
        {
            _logger.LogWarning("WebDAV COPY: Attempted to copy root node for user {UserId}", request.UserId);
            return new WebDavCopyResult(false, false, WebDavCopyError.CannotCopyRoot);
        }

        // Get destination parent
        var destParentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.DestinationPath, ct);
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV COPY: Destination parent not found: {Path}", request.DestinationPath);
            return new WebDavCopyResult(false, false, WebDavCopyError.DestinationParentNotFound);
        }

        // Validate new name
        if (!NameValidator.TryNormalizeAndValidate(destParentResult.ResourceName, out _, out var errorMessage))
        {
            _logger.LogDebug("WebDAV COPY: Invalid name: {Name}, Error: {Error}", destParentResult.ResourceName, errorMessage);
            return new WebDavCopyResult(false, false, WebDavCopyError.InvalidName);
        }

        var newNameKey = NameValidator.NormalizeAndGetNameKey(destParentResult.ResourceName);
        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);

        // Check if destination exists
        var destExists = await _pathResolver.ResolveMetadataAsync(request.UserId, request.DestinationPath, ct);
        if (destExists.Found && !request.Overwrite)
        {
            _logger.LogDebug("WebDAV COPY: Destination exists and overwrite is false: {Path}", request.DestinationPath);
            return new WebDavCopyResult(false, false, WebDavCopyError.DestinationExists);
        }

        bool created = !destExists.Found;

        // Handle overwrite by deleting existing destination
        if (destExists.Found && request.Overwrite)
        {
            if (destExists.IsCollection && destExists.Node is not null)
            {
                var nodeToDelete = await _dbContext.Nodes
                    .FirstOrDefaultAsync(n => n.Id == destExists.Node.Id, ct);
                if (nodeToDelete is not null)
                {
                    _dbContext.Nodes.Remove(nodeToDelete);
                }
            }
            else if (destExists.NodeFile is not null)
            {
                var fileToDelete = await _dbContext.NodeFiles
                    .FirstOrDefaultAsync(f => f.Id == destExists.NodeFile.Id, ct);
                if (fileToDelete is not null)
                {
                    _dbContext.NodeFiles.Remove(fileToDelete);
                }
            }
            await _dbContext.SaveChangesAsync(ct);
            created = true;
        }

        // Perform the copy
        if (sourceResult.IsCollection && sourceResult.Node is not null)
        {
            await CopyNodeRecursivelyAsync(
                sourceResult.Node.Id,
                destParentResult.ParentNode.Id,
                destParentResult.ResourceName,
                request.UserId,
                layout.Id,
                ct);
        }
        else if (sourceResult.NodeFile is not null)
        {
            var newNodeFile = new NodeFile
            {
                OwnerId = request.UserId,
                NodeId = destParentResult.ParentNode.Id,
                FileManifestId = sourceResult.NodeFile.FileManifestId,
                OriginalNodeFileId = sourceResult.NodeFile.OriginalNodeFileId
            };
            newNodeFile.SetName(destParentResult.ResourceName);

            await _dbContext.NodeFiles.AddAsync(newNodeFile, ct);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("WebDAV COPY: Copied {Source} to {Dest} for user {UserId}",
            request.SourcePath, request.DestinationPath, request.UserId);

        return new WebDavCopyResult(true, created);
    }

    private async Task CopyNodeRecursivelyAsync(
        Guid sourceNodeId,
        Guid destParentId,
        string newName,
        Guid userId,
        Guid layoutId,
        CancellationToken ct)
    {
        var sourceNode = await _dbContext.Nodes
            .AsNoTracking()
            .FirstAsync(n => n.Id == sourceNodeId, ct);

        // Create new node
        var newNode = new Node
        {
            OwnerId = userId,
            ParentId = destParentId,
            Type = sourceNode.Type,
            LayoutId = layoutId
        };
        newNode.SetName(newName);

        await _dbContext.Nodes.AddAsync(newNode, ct);
        await _dbContext.SaveChangesAsync(ct);

        // Copy child nodes
        var childNodes = await _dbContext.Nodes
            .AsNoTracking()
            .Where(n => n.ParentId == sourceNodeId)
            .ToListAsync(ct);

        foreach (var child in childNodes)
        {
            await CopyNodeRecursivelyAsync(child.Id, newNode.Id, child.Name, userId, layoutId, ct);
        }

        // Copy files
        var childFiles = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(f => f.NodeId == sourceNodeId)
            .ToListAsync(ct);

        foreach (var file in childFiles)
        {
            var newFile = new NodeFile
            {
                OwnerId = userId,
                NodeId = newNode.Id,
                FileManifestId = file.FileManifestId,
                OriginalNodeFileId = file.OriginalNodeFileId
            };
            newFile.SetName(file.Name);

            await _dbContext.NodeFiles.AddAsync(newFile, ct);
        }
    }
}
