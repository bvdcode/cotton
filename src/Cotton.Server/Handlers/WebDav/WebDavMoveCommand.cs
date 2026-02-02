// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Services.WebDav;
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
    CannotMoveRoot,
    CannotMoveIntoDescendant
}

/// <summary>
/// Handler for WebDAV MOVE operation
/// </summary>
public class WebDavMoveCommandHandler(
    CottonDbContext _dbContext,
    IMediator _mediator,
    IWebDavPathResolver _pathResolver,
    ILogger<WebDavMoveCommandHandler> _logger)
    : IRequestHandler<WebDavMoveCommand, WebDavMoveResult>
{
    public async Task<WebDavMoveResult> Handle(WebDavMoveCommand request, CancellationToken ct)
    {
        var sourceResult = await ResolveSourceAsync(request, ct);
        if (!sourceResult.Found)
        {
            return new WebDavMoveResult(false, false, WebDavMoveError.SourceNotFound);
        }

        if (sourceResult.IsCollection && sourceResult.Node?.ParentId is null)
        {
            _logger.LogWarning("WebDAV MOVE: Attempted to move root node for user {UserId}", request.UserId);
            return new WebDavMoveResult(false, false, WebDavMoveError.CannotMoveRoot);
        }

        var destParentResult = await GetAndValidateDestinationParentAsync(request, ct);
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            return new WebDavMoveResult(false, false, WebDavMoveError.DestinationParentNotFound);
        }

        var (created, allowed) = await HandleDestinationOverwriteAsync(request, ct);
        if (!allowed)
        {
            return new WebDavMoveResult(false, false, WebDavMoveError.DestinationExists);
        }

        var moved = await PerformMoveAsync(request, sourceResult, destParentResult, ct);
        if (!moved)
        {
            return new WebDavMoveResult(false, false, WebDavMoveError.CannotMoveIntoDescendant);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("WebDAV MOVE: Moved {Source} to {Dest} for user {UserId}",
            request.SourcePath, request.DestinationPath, request.UserId);

        return new WebDavMoveResult(true, created);
    }

    private async Task<WebDavResolveResult> ResolveSourceAsync(WebDavMoveCommand request, CancellationToken ct)
    {
        var sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
        if (!sourceResult.Found)
        {
            _logger.LogDebug("WebDAV MOVE: Source not found: {Path}", request.SourcePath);
        }
        return sourceResult;
    }

    private async Task<WebDavParentResult> GetAndValidateDestinationParentAsync(WebDavMoveCommand request, CancellationToken ct)
    {
        var destParentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.DestinationPath, ct);
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV MOVE: Destination parent not found: {Path}", request.DestinationPath);
            return destParentResult;
        }

        if (!NameValidator.TryNormalizeAndValidate(destParentResult.ResourceName, out _, out var errorMessage))
        {
            _logger.LogDebug("WebDAV MOVE: Invalid name: {Name}, Error: {Error}", destParentResult.ResourceName, errorMessage);
            return destParentResult with { Found = false };
        }

        return destParentResult;
    }

    private async Task<(bool Created, bool Allowed)> HandleDestinationOverwriteAsync(WebDavMoveCommand request, CancellationToken ct)
    {
        var destExists = await _pathResolver.ResolveMetadataAsync(request.UserId, request.DestinationPath, ct);
        if (destExists.Found && !request.Overwrite)
        {
            _logger.LogDebug("WebDAV MOVE: Destination exists and overwrite is false: {Path}", request.DestinationPath);
            return (false, false);
        }

        bool created = !destExists.Found;
        if (destExists.Found && request.Overwrite)
        {
            await DeleteExistingDestinationAsync(request.UserId, destExists, ct);
            created = true;
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

    private async Task<bool> PerformMoveAsync(
        WebDavMoveCommand request,
        WebDavResolveResult sourceResult,
        WebDavParentResult destParentResult,
        CancellationToken ct)
    {
        if (sourceResult.IsCollection && sourceResult.Node is not null)
        {
            if (await IsDescendantAsync(destParentResult.ParentNode!.Id, sourceResult.Node.Id, ct))
            {
                _logger.LogWarning("WebDAV MOVE: Attempted to move node {NodeId} into its descendant {DestParentId} for user {UserId}",
                    sourceResult.Node.Id, destParentResult.ParentNode.Id, request.UserId);
                return false;
            }

            var node = await _dbContext.Nodes
                .FirstAsync(n => n.Id == sourceResult.Node.Id, ct);

            node.ParentId = destParentResult.ParentNode!.Id;
            node.SetName(destParentResult.ResourceName!);
            return true;
        }

        if (sourceResult.NodeFile is not null)
        {
            var nodeFile = await _dbContext.NodeFiles
                .FirstAsync(f => f.Id == sourceResult.NodeFile.Id, ct);

            nodeFile.NodeId = destParentResult.ParentNode!.Id;
            nodeFile.SetName(destParentResult.ResourceName!);
        }

        return true;
    }

    private async Task<bool> IsDescendantAsync(Guid destParentId, Guid sourceNodeId, CancellationToken ct)
    {
        const int MaxDepth = 256;
        int depth = 0;
        Guid? currentId = destParentId;
        while (currentId.HasValue)
        {
            if (depth++ >= MaxDepth)
            {
                return true;
            }

            if (currentId.Value == sourceNodeId)
            {
                return true;
            }

            currentId = await _dbContext.Nodes
                .AsNoTracking()
                .Where(n => n.Id == currentId.Value)
                .Select(n => n.ParentId)
                .SingleOrDefaultAsync(ct);
        }

        return false;
    }
}
