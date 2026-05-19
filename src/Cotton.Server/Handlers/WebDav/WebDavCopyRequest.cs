// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
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
/// Command for WebDAV COPY operation
/// </summary>
public record WebDavCopyRequest(
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
    WebDavCopyError? Error = null,
    Guid? CopiedNodeId = null,
    Guid? CopiedNodeFileId = null);

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
public class WebDavCopyRequestHandler(
    CottonDbContext _dbContext,
    IMediator _mediator,
    IWebDavPathResolver _pathResolver,
    UserStorageQuotaService _quota,
    IEventNotificationService _eventNotification,
    ILogger<WebDavCopyRequestHandler> _logger)
    : IRequestHandler<WebDavCopyRequest, WebDavCopyResult>
{
    public async Task<WebDavCopyResult> Handle(WebDavCopyRequest request, CancellationToken ct)
    {
        var preLockSource = await ResolveSourceAsync(request, ct);
        var sourceValidation = ValidateSourceOrGetFailure(request, preLockSource);
        if (sourceValidation is not null)
        {
            return sourceValidation;
        }

        var destParentResult = await GetAndValidateDestinationParentAsync(request, ct);
        var destParentFailure = TryGetDestinationParentFailure(destParentResult);
        if (destParentFailure is not null)
        {
            return destParentFailure;
        }

        // Per-layout namespace serialization: COPY creates new entries in the
        // destination parent that can collide cross-table with a concurrent
        // create/move. For a recursive folder copy, the entire subtree creation
        // runs inside the lock - once an intermediate node hits the DB outside
        // the lock, concurrent operations can target it. Re-resolve source and
        // destination parent inside the lock so stale pre-lock objects never
        // drive the actual copy.
        Guid lockedLayoutId = destParentResult.ParentNode!.LayoutId;
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, lockedLayoutId, ct);

        var sourceResult = await ResolveSourceAsync(request, ct);
        sourceValidation = ValidateSourceOrGetFailure(request, sourceResult);
        if (sourceValidation is not null)
        {
            return sourceValidation;
        }

        if (await GetSourceLayoutIdAsync(sourceResult, ct) != lockedLayoutId)
        {
            _logger.LogDebug("WebDAV COPY: Source layout changed while waiting for lock: {Path}", request.SourcePath);
            return Fail(WebDavCopyError.SourceNotFound);
        }

        destParentResult = await GetAndValidateDestinationParentAsync(request, ct);
        destParentFailure = TryGetDestinationParentFailure(destParentResult);
        if (destParentFailure is not null)
        {
            return destParentFailure;
        }

        if (destParentResult.ParentNode!.LayoutId != lockedLayoutId)
        {
            _logger.LogDebug("WebDAV COPY: Destination parent layout changed while waiting for lock: {Path}", request.DestinationPath);
            return Fail(WebDavCopyError.DestinationParentNotFound);
        }

        var (created, allowed) = await HandleDestinationOverwriteAsync(request, ct);
        if (!allowed)
        {
            return Fail(WebDavCopyError.DestinationExists);
        }

        var (copiedNodeId, copiedNodeFileId, addedBytes) = await PerformCopyAsync(request, sourceResult, destParentResult, destParentResult.ParentNode!.LayoutId, ct);
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _quota.RecordLogicalBytesAdded(request.UserId, addedBytes);

        await NotifyCopyCompletedAsync(
            request,
            copiedNodeId,
            copiedNodeFileId,
            ct);
        return Ok(created, copiedNodeId, copiedNodeFileId);
    }

    private static WebDavCopyResult Fail(WebDavCopyError error)
    {
        return new WebDavCopyResult(false, false, error);
    }

    private static WebDavCopyResult Ok(bool created, Guid? copiedNodeId, Guid? copiedNodeFileId)
    {
        return new WebDavCopyResult(true, created, null, copiedNodeId, copiedNodeFileId);
    }

    private static WebDavCopyResult? TryGetDestinationParentFailure(WebDavParentResult destParentResult)
    {
        if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
        {
            return Fail(WebDavCopyError.DestinationParentNotFound);
        }

        return null;
    }

    private async Task NotifyCopyCompletedAsync(
        WebDavCopyRequest request,
        Guid? copiedNodeId,
        Guid? copiedNodeFileId,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "WebDAV COPY: Copied {Source} to {Dest} for user {UserId}",
            request.SourcePath,
            request.DestinationPath,
            request.UserId);

        if (copiedNodeId.HasValue)
        {
            await _eventNotification.NotifyNodeCreatedAsync(copiedNodeId.Value, ct);
        }
        else if (copiedNodeFileId.HasValue)
        {
            await _eventNotification.NotifyFileCreatedAsync(copiedNodeFileId.Value, ct);
        }
    }

    private WebDavCopyResult? ValidateSourceOrGetFailure(WebDavCopyRequest request, WebDavResolveResult sourceResult)
    {
        if (!sourceResult.Found)
        {
            return Fail(WebDavCopyError.SourceNotFound);
        }

        if (sourceResult.IsCollection && sourceResult.Node?.ParentId is null)
        {
            _logger.LogWarning("WebDAV COPY: Attempted to copy root node for user {UserId}", request.UserId);
            return Fail(WebDavCopyError.CannotCopyRoot);
        }

        return null;
    }

    private async Task<Guid> GetSourceLayoutIdAsync(WebDavResolveResult sourceResult, CancellationToken ct)
    {
        if (sourceResult.IsCollection)
        {
            return sourceResult.Node!.LayoutId;
        }

        return await _dbContext.Nodes
            .AsNoTracking()
            .Where(n => n.Id == sourceResult.NodeFile!.NodeId)
            .Select(n => n.LayoutId)
            .SingleAsync(ct);
    }

    private async Task<WebDavResolveResult> ResolveSourceAsync(WebDavCopyRequest request, CancellationToken ct)
    {
        var sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
        if (!sourceResult.Found)
        {
            _logger.LogDebug("WebDAV COPY: Source not found: {Path}", request.SourcePath);
        }
        return sourceResult;
    }

    private async Task<WebDavParentResult> GetAndValidateDestinationParentAsync(WebDavCopyRequest request, CancellationToken ct)
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

    private async Task<(bool Created, bool Allowed)> HandleDestinationOverwriteAsync(WebDavCopyRequest request, CancellationToken ct)
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

    private async Task<(Guid? NodeId, Guid? NodeFileId, long AddedBytes)> PerformCopyAsync(
        WebDavCopyRequest request,
        WebDavResolveResult sourceResult,
        WebDavParentResult destParentResult,
        Guid layoutId,
        CancellationToken ct)
    {
        if (sourceResult.IsCollection && sourceResult.Node is not null)
        {
            var (newNodeId, addedBytes) = await CopyNodeRecursivelyAsync(
                sourceResult.Node.Id,
                destParentResult.ParentNode!.Id,
                destParentResult.ResourceName!,
                request.UserId,
                layoutId,
                ct);
            return (newNodeId, null, addedBytes);
        }

        if (sourceResult.NodeFile is not null)
        {
            long addedBytes = await _quota.EnsureCanAddFileReferenceAsync(request.UserId, sourceResult.NodeFile.FileManifestId, ct);

            var newNodeFile = new NodeFile
            {
                OwnerId = request.UserId,
                NodeId = destParentResult.ParentNode!.Id,
                FileManifestId = sourceResult.NodeFile.FileManifestId,
            };
            newNodeFile.SetName(destParentResult.ResourceName!);

            await _dbContext.NodeFiles.AddAsync(newNodeFile, ct);
            await _dbContext.SaveChangesAsync(ct);
            newNodeFile.OriginalNodeFileId = newNodeFile.Id;
            return (null, newNodeFile.Id, addedBytes);
        }

        return (null, null, 0);
    }

    private async Task<(Guid NodeId, long AddedBytes)> CopyNodeRecursivelyAsync(
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

        long addedBytes = 0;

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
            var (_, childAddedBytes) = await CopyNodeRecursivelyAsync(child.Id, newNode.Id, child.Name, userId, layoutId, ct);
            addedBytes += childAddedBytes;
        }

        // Copy files
        var childFiles = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(f => f.NodeId == sourceNodeId && f.OwnerId == userId)
            .ToListAsync(ct);

        foreach (var file in childFiles)
        {
            addedBytes += await _quota.EnsureCanAddFileReferenceAsync(userId, file.FileManifestId, ct);

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

        return (newNode.Id, addedBytes);
    }
}
