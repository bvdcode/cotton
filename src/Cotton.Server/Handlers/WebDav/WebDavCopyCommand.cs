// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
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
    IMediator _mediator,
    IWebDavPathResolver _pathResolver,
    ILogger<WebDavCopyCommandHandler> _logger)
    : IRequestHandler<WebDavCopyCommand, WebDavCopyResult>
{
    public async Task<WebDavCopyResult> Handle(WebDavCopyCommand request, CancellationToken ct)
    {
        var sourceResult = await ResolveSourceAsync(request, ct);
        if (!sourceResult.Found)
        {
            return new WebDavCopyResult(false, false, WebDavCopyError.SourceNotFound);
        }

        if (sourceResult.IsCollection && sourceResult.Node?.ParentId is null)
        {
            _logger.LogWarning("WebDAV COPY: Attempted to copy root node for user {UserId}", request.UserId);
            return new WebDavCopyResult(false, false, WebDavCopyError.CannotCopyRoot);
        }

        var destParentResult = await GetAndValidateDestinationParentAsync(request, ct);
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            return new WebDavCopyResult(false, false, WebDavCopyError.DestinationParentNotFound);
        }

        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);
        var (created, allowed) = await HandleDestinationOverwriteAsync(request, ct);
        if (!allowed)
        {
            return new WebDavCopyResult(false, false, WebDavCopyError.DestinationExists);
        }

        await PerformCopyAsync(request, sourceResult, destParentResult, layout.Id, ct);
        await _dbContext.SaveChangesAsync(ct);

        if (sourceResult.NodeFile is not null)
        {
            await EnsureNewVersionFamilyAsync(request.UserId, destParentResult.ParentNode.Id, destParentResult.ResourceName, ct);
        }

        _logger.LogInformation("WebDAV COPY: Copied {Source} to {Dest} for user {UserId}",
            request.SourcePath, request.DestinationPath, request.UserId);

        return new WebDavCopyResult(true, created);
    }

    private async Task<WebDavResolveResult> ResolveSourceAsync(WebDavCopyCommand request, CancellationToken ct)
    {
        var sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
        if (!sourceResult.Found)
        {
            _logger.LogDebug("WebDAV COPY: Source not found: {Path}", request.SourcePath);
        }
        return sourceResult;
    }

    private async Task<WebDavParentResult> GetAndValidateDestinationParentAsync(WebDavCopyCommand request, CancellationToken ct)
    {
        var destParentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.DestinationPath, ct);
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV COPY: Destination parent not found: {Path}", request.DestinationPath);
            return destParentResult;
        }

        if (!NameValidator.TryNormalizeAndValidate(destParentResult.ResourceName, out _, out var errorMessage))
        {
            _logger.LogDebug("WebDAV COPY: Invalid name: {Name}, Error: {Error}", destParentResult.ResourceName, errorMessage);
            return destParentResult with { Found = false };
        }

        return destParentResult;
    }

    private async Task<(bool Created, bool Allowed)> HandleDestinationOverwriteAsync(WebDavCopyCommand request, CancellationToken ct)
    {
        var destExists = await _pathResolver.ResolveMetadataAsync(request.UserId, request.DestinationPath, ct);
        if (destExists.Found && !request.Overwrite)
        {
            _logger.LogDebug("WebDAV COPY: Destination exists and overwrite is false: {Path}", request.DestinationPath);
            return (false, false);
        }

        bool created = !destExists.Found;
        if (destExists.Found && request.Overwrite)
        {
            await DeleteExistingDestinationAsync(request.UserId, destExists, ct);
        }

        return (created, true);
    }

    private async Task DeleteExistingDestinationAsync(Guid userId, WebDavResolveResult destination, CancellationToken ct)
    {
        if (destination.IsCollection && destination.Node is not null)
        {
            await _mediator.Send(new DeleteNodeQuery(userId, destination.Node.Id, skipTrash: false), ct);
            return;
        }

        if (destination.NodeFile is not null)
        {
            await _mediator.Send(new DeleteFileQuery(userId, destination.NodeFile.Id, skipTrash: false), ct);
        }
    }

    private async Task PerformCopyAsync(
        WebDavCopyCommand request,
        WebDavResolveResult sourceResult,
        WebDavParentResult destParentResult,
        Guid layoutId,
        CancellationToken ct)
    {
        if (sourceResult.IsCollection && sourceResult.Node is not null)
        {
            await CopyNodeRecursivelyAsync(
                sourceResult.Node.Id,
                destParentResult.ParentNode!.Id,
                destParentResult.ResourceName!,
                request.UserId,
                layoutId,
                ct);
            return;
        }

        if (sourceResult.NodeFile is not null)
        {
            var newNodeFile = new NodeFile
            {
                OwnerId = request.UserId,
                NodeId = destParentResult.ParentNode!.Id,
                FileManifestId = sourceResult.NodeFile.FileManifestId,
            };
            newNodeFile.SetName(destParentResult.ResourceName!);

            await _dbContext.NodeFiles.AddAsync(newNodeFile, ct);
        }
    }

    private async Task EnsureNewVersionFamilyAsync(Guid userId, Guid destParentNodeId, string resourceName, CancellationToken ct)
    {
        var createdFile = await _dbContext.NodeFiles
            .Where(f => f.OwnerId == userId
                && f.NodeId == destParentNodeId
                && f.NameKey == NameValidator.NormalizeAndGetNameKey(resourceName))
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (createdFile is not null && createdFile.OriginalNodeFileId == Guid.Empty)
        {
            createdFile.OriginalNodeFileId = createdFile.Id;
            await _dbContext.SaveChangesAsync(ct);
        }
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
            .Where(n => n.ParentId == sourceNodeId
                && n.OwnerId == userId
                && n.LayoutId == layoutId
                && n.Type == sourceNode.Type)
            .ToListAsync(ct);

        foreach (var child in childNodes)
        {
            await CopyNodeRecursivelyAsync(child.Id, newNode.Id, child.Name, userId, layoutId, ct);
        }

        // Copy files
        var childFiles = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(f => f.NodeId == sourceNodeId && f.OwnerId == userId)
            .ToListAsync(ct);

        foreach (var file in childFiles)
        {
            var newFile = new NodeFile
            {
                OwnerId = userId,
                NodeId = newNode.Id,
                FileManifestId = file.FileManifestId
            };
            newFile.SetName(file.Name);

            await _dbContext.NodeFiles.AddAsync(newFile, ct);
            newFile.OriginalNodeFileId = newFile.Id;
        }
    }
}
