// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services.WebDav;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV MOVE/COPY operation
/// </summary>
public record WebDavMoveCommand(
    Guid UserId,
    string SourcePath,
    string DestinationPath,
    bool Overwrite = false) : IRequest<WebDavMoveResult>;

/// <summary>
/// Result of WebDAV MOVE operation
/// </summary>
public record WebDavMoveResult(
    bool Success,
    bool Created,
    WebDavMoveError? Error = null);

public enum WebDavMoveError
{
    SourceNotFound,
    DestinationParentNotFound,
    DestinationExists,
    InvalidName,
    CannotMoveRoot
}

/// <summary>
/// Handler for WebDAV MOVE operation
/// </summary>
public class WebDavMoveCommandHandler(
    CottonDbContext _dbContext,
    IWebDavPathResolver _pathResolver,
    ILogger<WebDavMoveCommandHandler> _logger)
    : IRequestHandler<WebDavMoveCommand, WebDavMoveResult>
{
    public async Task<WebDavMoveResult> Handle(WebDavMoveCommand request, CancellationToken ct)
    {
        // Resolve source
        var sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
        if (!sourceResult.Found)
        {
            _logger.LogDebug("WebDAV MOVE: Source not found: {Path}", request.SourcePath);
            return new WebDavMoveResult(false, false, WebDavMoveError.SourceNotFound);
        }

        // Can't move root
        if (sourceResult.IsCollection && sourceResult.Node?.ParentId is null)
        {
            _logger.LogWarning("WebDAV MOVE: Attempted to move root node for user {UserId}", request.UserId);
            return new WebDavMoveResult(false, false, WebDavMoveError.CannotMoveRoot);
        }

        // Get destination parent
        var destParentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.DestinationPath, ct);
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV MOVE: Destination parent not found: {Path}", request.DestinationPath);
            return new WebDavMoveResult(false, false, WebDavMoveError.DestinationParentNotFound);
        }

        // Validate new name
        if (!NameValidator.TryNormalizeAndValidate(destParentResult.ResourceName, out _, out var errorMessage))
        {
            _logger.LogDebug("WebDAV MOVE: Invalid name: {Name}, Error: {Error}", destParentResult.ResourceName, errorMessage);
            return new WebDavMoveResult(false, false, WebDavMoveError.InvalidName);
        }

        // Check if destination exists
        var destExists = await _pathResolver.ResolveMetadataAsync(request.UserId, request.DestinationPath, ct);
        if (destExists.Found && !request.Overwrite)
        {
            _logger.LogDebug("WebDAV MOVE: Destination exists and overwrite is false: {Path}", request.DestinationPath);
            return new WebDavMoveResult(false, false, WebDavMoveError.DestinationExists);
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
            created = true;
        }

        // Perform the move
        if (sourceResult.IsCollection && sourceResult.Node is not null)
        {
            var node = await _dbContext.Nodes
                .FirstAsync(n => n.Id == sourceResult.Node.Id, ct);

            node.ParentId = destParentResult.ParentNode.Id;
            node.SetName(destParentResult.ResourceName);
        }
        else if (sourceResult.NodeFile is not null)
        {
            var nodeFile = await _dbContext.NodeFiles
                .FirstAsync(f => f.Id == sourceResult.NodeFile.Id, ct);

            nodeFile.NodeId = destParentResult.ParentNode.Id;
            nodeFile.SetName(destParentResult.ResourceName);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("WebDAV MOVE: Moved {Source} to {Dest} for user {UserId}",
            request.SourcePath, request.DestinationPath, request.UserId);

        return new WebDavMoveResult(true, created);
    }
}
