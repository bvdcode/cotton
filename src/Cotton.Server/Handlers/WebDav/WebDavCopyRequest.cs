// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Shared.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
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

/// <summary>
/// Lists the supported web dav copy error values.
/// </summary>
public enum WebDavCopyError
{
    /// <summary>
    /// Represents the source not found option.
    /// </summary>
    SourceNotFound,
    /// <summary>
    /// Represents the destination parent not found option.
    /// </summary>
    DestinationParentNotFound,
    /// <summary>
    /// Represents the destination exists option.
    /// </summary>
    DestinationExists,
    /// <summary>
    /// Represents the invalid name option.
    /// </summary>
    InvalidName,
    /// <summary>
    /// Represents the cannot copy root option.
    /// </summary>
    CannotCopyRoot,
    /// <summary>
    /// Represents the quota exceeded option.
    /// </summary>
    QuotaExceeded
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
    ISyncChangeRecorder _syncChanges,
    ILogger<WebDavCopyRequestHandler> _logger)
    : IRequestHandler<WebDavCopyRequest, WebDavCopyResult>
{
    /// <summary>
    /// Handles the request through the mediator pipeline.
    /// </summary>
    public async Task<WebDavCopyResult> Handle(WebDavCopyRequest request, CancellationToken ct)
    {
        var preconditions = await ResolvePreLockPreconditionsAsync(request, ct);
        if (preconditions.Failure is not null)
        {
            return preconditions.Failure;
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, preconditions.LayoutId!.Value, ct);

        var lockedCopy = await PrepareLockedCopyAsync(request, preconditions.LayoutId.Value, ct);
        if (lockedCopy.Failure is not null)
        {
            return lockedCopy.Failure;
        }

        var copyResult = await TryPerformCopyAsync(request, lockedCopy.Source!, lockedCopy.DestinationParent!, ct);
        if (copyResult.Failure is not null)
        {
            return copyResult.Failure;
        }

        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _quota.RecordLogicalBytesAdded(request.UserId, copyResult.AddedBytes);

        await NotifyCopyCompletedAsync(request, copyResult.NodeId, copyResult.NodeFileId, ct);
        return Ok(lockedCopy.Created, copyResult.NodeId, copyResult.NodeFileId);
    }

    private async Task<PreLockCopyOutcome> ResolvePreLockPreconditionsAsync(WebDavCopyRequest request, CancellationToken ct)
    {
        var source = await ResolveSourceAsync(request, ct);
        var sourceFailure = ValidateSourceOrGetFailure(request, source);
        if (sourceFailure is not null)
        {
            return PreLockCopyOutcome.Failed(sourceFailure);
        }

        var destinationParent = await GetAndValidateDestinationParentAsync(request, ct);
        var destinationFailure = TryGetDestinationParentFailure(destinationParent);
        return destinationFailure is null
            ? PreLockCopyOutcome.Success(destinationParent.ParentNode!.LayoutId)
            : PreLockCopyOutcome.Failed(destinationFailure);
    }

    private async Task<LockedCopyOutcome> PrepareLockedCopyAsync(
        WebDavCopyRequest request,
        Guid lockedLayoutId,
        CancellationToken ct)
    {
        var source = await ResolveSourceAsync(request, ct);
        var sourceFailure = await ValidateLockedSourceAsync(request, source, lockedLayoutId, ct);
        if (sourceFailure is not null)
        {
            return LockedCopyOutcome.Failed(sourceFailure);
        }

        var destinationParent = await GetAndValidateDestinationParentAsync(request, ct);
        var destinationFailure = ValidateLockedDestination(destinationParent, request, lockedLayoutId);
        if (destinationFailure is not null)
        {
            return LockedCopyOutcome.Failed(destinationFailure);
        }

        var overwriteResult = await HandleDestinationOverwriteAsync(request, ct);
        return overwriteResult.Allowed
            ? LockedCopyOutcome.Success(source, destinationParent, overwriteResult.Created)
            : LockedCopyOutcome.Failed(Fail(WebDavCopyError.DestinationExists));
    }

    private async Task<WebDavCopyResult?> ValidateLockedSourceAsync(
        WebDavCopyRequest request,
        WebDavResolveResult source,
        Guid lockedLayoutId,
        CancellationToken ct)
    {
        var sourceFailure = ValidateSourceOrGetFailure(request, source);
        if (sourceFailure is not null)
        {
            return sourceFailure;
        }

        if (await GetSourceLayoutIdAsync(source, ct) == lockedLayoutId)
        {
            return null;
        }

        _logger.LogDebug("WebDAV COPY: Source layout changed while waiting for lock: {Path}", request.SourcePath);
        return Fail(WebDavCopyError.SourceNotFound);
    }

    private WebDavCopyResult? ValidateLockedDestination(
        WebDavParentResult destinationParent,
        WebDavCopyRequest request,
        Guid lockedLayoutId)
    {
        var destinationFailure = TryGetDestinationParentFailure(destinationParent);
        if (destinationFailure is not null)
        {
            return destinationFailure;
        }

        if (destinationParent.ParentNode!.LayoutId == lockedLayoutId)
        {
            return null;
        }

        _logger.LogDebug("WebDAV COPY: Destination parent layout changed while waiting for lock: {Path}", request.DestinationPath);
        return Fail(WebDavCopyError.DestinationParentNotFound);
    }

    private async Task<CopyOperationOutcome> TryPerformCopyAsync(
        WebDavCopyRequest request,
        WebDavResolveResult source,
        WebDavParentResult destinationParent,
        CancellationToken ct)
    {
        try
        {
            var (nodeId, nodeFileId, addedBytes) = await PerformCopyAsync(
                request,
                source,
                destinationParent,
                destinationParent.ParentNode!.LayoutId,
                ct);
            return CopyOperationOutcome.Success(nodeId, nodeFileId, addedBytes);
        }
        catch (StorageQuotaExceededException<User>)
        {
            return CopyOperationOutcome.Failed(Fail(WebDavCopyError.QuotaExceeded));
        }
    }

    private sealed record PreLockCopyOutcome(Guid? LayoutId, WebDavCopyResult? Failure)
    {
        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        public static PreLockCopyOutcome Success(Guid layoutId) => new(layoutId, null);
        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        public static PreLockCopyOutcome Failed(WebDavCopyResult failure) => new(null, failure);
    }

    private sealed record LockedCopyOutcome(
        WebDavResolveResult? Source,
        WebDavParentResult? DestinationParent,
        bool Created,
        WebDavCopyResult? Failure)
    {
        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        public static LockedCopyOutcome Success(
            WebDavResolveResult source,
            WebDavParentResult destinationParent,
            bool created) => new(source, destinationParent, created, null);

        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        public static LockedCopyOutcome Failed(WebDavCopyResult failure) => new(null, null, false, failure);
    }

    private sealed record CopyOperationOutcome(
        Guid? NodeId,
        Guid? NodeFileId,
        long AddedBytes,
        WebDavCopyResult? Failure)
    {
        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        public static CopyOperationOutcome Success(Guid? nodeId, Guid? nodeFileId, long addedBytes) =>
            new(nodeId, nodeFileId, addedBytes, null);

        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        public static CopyOperationOutcome Failed(WebDavCopyResult failure) => new(null, null, 0, failure);
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
                destParentResult.ParentNode!,
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
            _syncChanges.StageFileChange(
                SyncChangeKind.FileCreated,
                newNodeFile,
                destParentResult.ParentNode.LayoutId);
            return (null, newNodeFile.Id, addedBytes);
        }

        return (null, null, 0);
    }

    private async Task<(Guid NodeId, long AddedBytes)> CopyNodeRecursivelyAsync(
        Guid sourceNodeId,
        Node destParent,
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
            Type = sourceNode.Type,
            LayoutId = layoutId
        };
        newNode.SetParent(destParent, sourceNode.Type);
        newNode.SetName(newName);

        await _dbContext.Nodes.AddAsync(newNode, ct);
        _syncChanges.StageFolderChange(SyncChangeKind.FolderCreated, newNode, destParent.Id);
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
            var (_, childAddedBytes) = await CopyNodeRecursivelyAsync(child.Id, newNode, child.Name, userId, layoutId, ct);
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
            _syncChanges.StageFileChange(SyncChangeKind.FileCreated, newFile, layoutId);
        }

        return (newNode.Id, addedBytes);
    }
}
