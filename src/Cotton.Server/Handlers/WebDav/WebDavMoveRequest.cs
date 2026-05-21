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
        var preLock = await ResolvePreLockSourceAsync(request, ct);
        if (preLock.Failure is not null)
        {
            return preLock.Failure;
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, preLock.LayoutId!.Value, ct);

        var lockedMove = await PrepareLockedMoveAsync(request, preLock.LayoutId.Value, ct);
        if (lockedMove.Failure is not null)
        {
            return lockedMove.Failure;
        }

        var moveFailure = await TryPerformMoveAsync(request, lockedMove.Source!, lockedMove.DestinationParent!, ct);
        if (moveFailure is not null)
        {
            return moveFailure;
        }

        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await NotifyAfterCommitAsync(request, lockedMove.Source!, lockedMove.OldParentId, ct);

        var movedIds = GetMovedIds(lockedMove.Source!);
        return Ok(lockedMove.Created, movedIds.NodeId, movedIds.NodeFileId);
    }

    private async Task<PreLockSourceOutcome> ResolvePreLockSourceAsync(WebDavMoveRequest request, CancellationToken ct)
    {
        var source = await ResolveSourceAsync(request, ct);
        var failure = TryGetSourceValidationFailure(request, source);
        if (failure is not null)
        {
            return PreLockSourceOutcome.Failed(failure);
        }

        Guid layoutId = await GetSourceLayoutIdAsync(source, ct);
        return PreLockSourceOutcome.Success(layoutId);
    }

    private async Task<LockedMoveOutcome> PrepareLockedMoveAsync(
        WebDavMoveRequest request,
        Guid sourceLayoutId,
        CancellationToken ct)
    {
        var source = await ResolveSourceAsync(request, ct);
        var sourceFailure = await ValidateLockedSourceAsync(request, source, sourceLayoutId, ct);
        if (sourceFailure is not null)
        {
            return LockedMoveOutcome.Failed(sourceFailure);
        }

        var destination = await GetAndValidateDestinationParentAsync(request, ct);
        var destinationFailure = ValidateLockedDestination(destination, request, sourceLayoutId);
        if (destinationFailure is not null)
        {
            return LockedMoveOutcome.Failed(destinationFailure);
        }

        var overwriteResult = await HandleDestinationOverwriteAsync(request, ct);
        if (!overwriteResult.Allowed)
        {
            return LockedMoveOutcome.Failed(Fail(WebDavMoveError.DestinationExists));
        }

        return LockedMoveOutcome.Success(source, destination, overwriteResult.Created, GetOldParentId(source));
    }

    private async Task<WebDavMoveResult?> ValidateLockedSourceAsync(
        WebDavMoveRequest request,
        WebDavResolveResult source,
        Guid sourceLayoutId,
        CancellationToken ct)
    {
        var sourceFailure = TryGetSourceValidationFailure(request, source);
        if (sourceFailure is not null)
        {
            return sourceFailure;
        }

        if (await GetSourceLayoutIdAsync(source, ct) == sourceLayoutId)
        {
            return null;
        }

        _logger.LogDebug("WebDAV MOVE: Source layout changed while waiting for lock: {Path}", request.SourcePath);
        return Fail(WebDavMoveError.SourceNotFound);
    }

    private WebDavMoveResult? ValidateLockedDestination(
        WebDavParentResult destination,
        WebDavMoveRequest request,
        Guid sourceLayoutId)
    {
        var destinationFailure = TryGetDestinationParentFailure(destination);
        if (destinationFailure is not null)
        {
            return destinationFailure;
        }

        if (destination.ParentNode!.LayoutId == sourceLayoutId)
        {
            return null;
        }

        _logger.LogDebug("WebDAV MOVE: Destination parent layout differs from locked source layout: {Path}", request.DestinationPath);
        return Fail(WebDavMoveError.DestinationParentNotFound);
    }

    private static Guid? GetOldParentId(WebDavResolveResult source)
    {
        return source.IsCollection
            ? source.Node?.ParentId
            : source.NodeFile?.NodeId;
    }

    private sealed record PreLockSourceOutcome(Guid? LayoutId, WebDavMoveResult? Failure)
    {
        public static PreLockSourceOutcome Success(Guid layoutId) => new(layoutId, null);
        public static PreLockSourceOutcome Failed(WebDavMoveResult failure) => new(null, failure);
    }

    private sealed record LockedMoveOutcome(
        WebDavResolveResult? Source,
        WebDavParentResult? DestinationParent,
        bool Created,
        Guid? OldParentId,
        WebDavMoveResult? Failure)
    {
        public static LockedMoveOutcome Success(
            WebDavResolveResult source,
            WebDavParentResult destinationParent,
            bool created,
            Guid? oldParentId) => new(source, destinationParent, created, oldParentId, null);

        public static LockedMoveOutcome Failed(WebDavMoveResult failure) => new(null, null, false, null, failure);
    }

    private async Task<Guid> GetSourceLayoutIdAsync(WebDavResolveResult source, CancellationToken ct)
    {
        // The resolver does not include Node on NodeFile, so we look the file's
        // layout up by its parent NodeId.
        if (source.IsCollection)
        {
            return source.Node!.LayoutId;
        }
        return await _dbContext.Nodes
            .AsNoTracking()
            .Where(n => n.Id == source.NodeFile!.NodeId)
            .Select(n => n.LayoutId)
            .SingleAsync(ct);
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

    private async Task NotifyAfterCommitAsync(
        WebDavMoveRequest request,
        WebDavResolveResult sourceResult,
        Guid? oldParentId,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "WebDAV MOVE: Moved {Source} to {Dest} for user {UserId}",
            request.SourcePath,
            request.DestinationPath,
            request.UserId);

        var movedIds = GetMovedIds(sourceResult);
        // Best-effort: a notification failure must not turn an already-committed move into a failed response.
        try
        {
            if (movedIds.NodeId.HasValue && oldParentId.HasValue)
            {
                await _eventNotification.NotifyNodeMovedAsync(movedIds.NodeId.Value, oldParentId.Value, ct);
            }
            else if (movedIds.NodeFileId.HasValue && oldParentId.HasValue)
            {
                await _eventNotification.NotifyFileMovedAsync(movedIds.NodeFileId.Value, oldParentId.Value, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "WebDAV MOVE: notification failed after committed move (NodeId={NodeId}, NodeFileId={NodeFileId})",
                movedIds.NodeId, movedIds.NodeFileId);
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

            node.SetParent(destParentResult.ParentNode!);
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
