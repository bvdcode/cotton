// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV MOVE/COPY operation
/// </summary>
public record WebDavMoveRequest(
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
    WebDavMoveError? Error = null,
    Guid? MovedNodeId = null,
    Guid? MovedNodeFileId = null);

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
public class WebDavMoveRequestHandler(
    CottonDbContext _dbContext,
    IMediator _mediator,
    IWebDavPathResolver _pathResolver,
    IEventNotificationService _eventNotification,
    ILogger<WebDavMoveRequestHandler> _logger)
    : IRequestHandler<WebDavMoveRequest, WebDavMoveResult>
{
    public async Task<WebDavMoveResult> Handle(WebDavMoveRequest request, CancellationToken ct)
    {
        var sourceResult = await ResolveSourceAsync(request, ct);
        var sourceValidationFailure = TryGetSourceValidationFailure(request, sourceResult);
        if (sourceValidationFailure is not null)
        {
            return sourceValidationFailure;
        }

        var destParentResult = await GetAndValidateDestinationParentAsync(request, ct);
        var destParentFailure = TryGetDestinationParentFailure(destParentResult);
        if (destParentFailure is not null)
        {
            return destParentFailure;
        }

        var overwriteResult = await HandleDestinationOverwriteAsync(request, ct);
        if (!overwriteResult.Allowed)
        {
            return Fail(WebDavMoveError.DestinationExists);
        }

        var moveFailure = await TryPerformMoveAsync(request, sourceResult, destParentResult, ct);
        if (moveFailure is not null)
        {
            return moveFailure;
        }

        await PersistAndNotifyAsync(request, sourceResult, ct);

        var movedIds = GetMovedIds(sourceResult);
        return Ok(overwriteResult.Created, movedIds.NodeId, movedIds.NodeFileId);
    }

    private static WebDavMoveResult Ok(bool created, Guid? movedNodeId, Guid? movedNodeFileId) =>
        new(true, created, null, movedNodeId, movedNodeFileId);

    private static WebDavMoveResult Fail(WebDavMoveError error) =>
        new(false, false, error);

    private WebDavMoveResult? TryGetSourceValidationFailure(WebDavMoveRequest request, WebDavResolveResult sourceResult)
    {
        if (!sourceResult.Found)
        {
            return Fail(WebDavMoveError.SourceNotFound);
        }

        if (sourceResult.IsCollection && sourceResult.Node?.ParentId is null)
        {
            _logger.LogWarning("WebDAV MOVE: Attempted to move root node for user {UserId}", request.UserId);
            return Fail(WebDavMoveError.CannotMoveRoot);
        }

        return null;
    }

    private static WebDavMoveResult? TryGetDestinationParentFailure(WebDavParentResult destParentResult)
    {
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            return Fail(WebDavMoveError.DestinationParentNotFound);
        }

        return null;
    }

    private static (Guid? NodeId, Guid? NodeFileId) GetMovedIds(WebDavResolveResult sourceResult)
    {
        var movedNodeId = sourceResult.IsCollection && sourceResult.Node is not null
            ? sourceResult.Node.Id
            : (Guid?)null;

        return (movedNodeId, sourceResult.NodeFile?.Id);
    }

    private async Task PersistAndNotifyAsync(
        WebDavMoveRequest request,
        WebDavResolveResult sourceResult,
        CancellationToken ct)
    {
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "WebDAV MOVE: Moved {Source} to {Dest} for user {UserId}",
            request.SourcePath,
            request.DestinationPath,
            request.UserId);

        var movedIds = GetMovedIds(sourceResult);
        if (movedIds.NodeId.HasValue)
        {
            await _eventNotification.NotifyNodeMovedAsync(movedIds.NodeId.Value, ct);
        }
        else if (movedIds.NodeFileId.HasValue)
        {
            await _eventNotification.NotifyFileMovedAsync(movedIds.NodeFileId.Value, ct);
        }
    }

    private async Task<WebDavResolveResult> ResolveSourceAsync(WebDavMoveRequest request, CancellationToken ct)
    {
        var sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
        if (!sourceResult.Found)
        {
            _logger.LogDebug("WebDAV MOVE: Source not found: {Path}", request.SourcePath);
        }
        return sourceResult;
    }

    private async Task<WebDavParentResult> GetAndValidateDestinationParentAsync(WebDavMoveRequest request, CancellationToken ct)
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

    private async Task<(bool Created, bool Allowed)> HandleDestinationOverwriteAsync(WebDavMoveRequest request, CancellationToken ct)
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

    private async Task<WebDavMoveResult?> TryPerformMoveAsync(
        WebDavMoveRequest request,
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
                return Fail(WebDavMoveError.CannotMoveIntoDescendant);
            }

            var node = await _dbContext.Nodes
                .FirstAsync(n => n.Id == sourceResult.Node.Id, ct);

            node.ParentId = destParentResult.ParentNode!.Id;
            node.SetName(destParentResult.ResourceName!);
            return null;
        }

        if (sourceResult.NodeFile is not null)
        {
            var nodeFile = await _dbContext.NodeFiles
                .FirstAsync(f => f.Id == sourceResult.NodeFile.Id, ct);

            nodeFile.NodeId = destParentResult.ParentNode!.Id;
            nodeFile.SetName(destParentResult.ResourceName!);
        }

        return null;
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
