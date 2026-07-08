// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
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
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Command for WebDAV COPY operation
    /// </summary>
    public record WebDavCopyRequest(
        Guid UserId,
        string SourcePath,
        string DestinationPath,
        bool Overwrite = false) : IRequest<WebDavCopyResult>;

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
        ILayoutMutationGate _layoutGate,
        ILogger<WebDavCopyRequestHandler> _logger)
        : IRequestHandler<WebDavCopyRequest, WebDavCopyResult>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<WebDavCopyResult> Handle(WebDavCopyRequest request, CancellationToken ct)
        {
            PreTransactionCopyOutcome preTransaction = await ResolvePreTransactionPreconditionsAsync(request, ct);
            if (preTransaction.Failure is not null)
            {
                return preTransaction.Failure;
            }

            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(preTransaction.LayoutId!.Value, ct);
            await using IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync(ct);

            CopyPreparationOutcome preparedCopy = await PrepareCopyInTransactionAsync(request, preTransaction.LayoutId.Value, ct);
            if (preparedCopy.Failure is not null)
            {
                return preparedCopy.Failure;
            }

            CopyOperationOutcome copyResult = await TryPerformCopyAsync(request, preparedCopy.Source!, preparedCopy.DestinationParent!, ct);
            if (copyResult.Failure is not null)
            {
                return copyResult.Failure;
            }

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            _quota.RecordLogicalBytesAdded(request.UserId, copyResult.AddedBytes);

            await NotifyCopyCompletedAsync(request, copyResult.NodeId, copyResult.NodeFileId, ct);
            return Ok(preparedCopy.Created, copyResult.NodeId, copyResult.NodeFileId);
        }

        private async Task<PreTransactionCopyOutcome> ResolvePreTransactionPreconditionsAsync(WebDavCopyRequest request, CancellationToken ct)
        {
            WebDavResolveResult source = await ResolveSourceAsync(request, ct);
            WebDavCopyResult? sourceFailure = ValidateSourceOrGetFailure(request, source);
            if (sourceFailure is not null)
            {
                return PreTransactionCopyOutcome.Failed(sourceFailure);
            }

            WebDavParentResult destinationParent = await GetAndValidateDestinationParentAsync(request, ct);
            WebDavCopyResult? destinationFailure = TryGetDestinationParentFailure(destinationParent);
            return destinationFailure is null
                ? PreTransactionCopyOutcome.Success(destinationParent.ParentNode!.LayoutId)
                : PreTransactionCopyOutcome.Failed(destinationFailure);
        }

        private async Task<CopyPreparationOutcome> PrepareCopyInTransactionAsync(
            WebDavCopyRequest request,
            Guid expectedLayoutId,
            CancellationToken ct)
        {
            WebDavResolveResult source = await ResolveSourceAsync(request, ct);
            WebDavCopyResult? sourceFailure = await ValidateSourceInTransactionAsync(request, source, expectedLayoutId, ct);
            if (sourceFailure is not null)
            {
                return CopyPreparationOutcome.Failed(sourceFailure);
            }

            WebDavParentResult destinationParent = await GetAndValidateDestinationParentAsync(request, ct);
            WebDavCopyResult? destinationFailure = ValidateDestinationInTransaction(destinationParent, request, expectedLayoutId);
            if (destinationFailure is not null)
            {
                return CopyPreparationOutcome.Failed(destinationFailure);
            }

            (bool Created, bool Allowed) overwriteResult = await HandleDestinationOverwriteAsync(request, ct);
            return overwriteResult.Allowed
                ? CopyPreparationOutcome.Success(source, destinationParent, overwriteResult.Created)
                : CopyPreparationOutcome.Failed(Fail(WebDavCopyError.DestinationExists));
        }

        private async Task<WebDavCopyResult?> ValidateSourceInTransactionAsync(
            WebDavCopyRequest request,
            WebDavResolveResult source,
            Guid expectedLayoutId,
            CancellationToken ct)
        {
            WebDavCopyResult? sourceFailure = ValidateSourceOrGetFailure(request, source);
            if (sourceFailure is not null)
            {
                return sourceFailure;
            }

            if (await GetSourceLayoutIdAsync(source, ct) == expectedLayoutId)
            {
                return null;
            }

            _logger.LogDebug("WebDAV COPY: Source layout changed during copy preparation: {Path}", request.SourcePath);
            return Fail(WebDavCopyError.SourceNotFound);
        }

        private WebDavCopyResult? ValidateDestinationInTransaction(
            WebDavParentResult destinationParent,
            WebDavCopyRequest request,
            Guid expectedLayoutId)
        {
            WebDavCopyResult? destinationFailure = TryGetDestinationParentFailure(destinationParent);
            if (destinationFailure is not null)
            {
                return destinationFailure;
            }

            if (destinationParent.ParentNode!.LayoutId == expectedLayoutId)
            {
                return null;
            }

            _logger.LogDebug("WebDAV COPY: Destination parent layout changed during copy preparation: {Path}", request.DestinationPath);
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

        private record PreTransactionCopyOutcome(Guid? LayoutId, WebDavCopyResult? Failure)
        {
            /// <summary>
            /// Creates a successful operation result.
            /// </summary>
            public static PreTransactionCopyOutcome Success(Guid layoutId) => new(layoutId, null);

            /// <summary>
            /// Creates a failed operation result.
            /// </summary>
            public static PreTransactionCopyOutcome Failed(WebDavCopyResult failure) => new(null, failure);
        }

        private record CopyPreparationOutcome(
            WebDavResolveResult? Source,
            WebDavParentResult? DestinationParent,
            bool Created,
            WebDavCopyResult? Failure)
        {
            /// <summary>
            /// Creates a successful operation result.
            /// </summary>
            public static CopyPreparationOutcome Success(
                WebDavResolveResult source,
                WebDavParentResult destinationParent,
                bool created) => new(source, destinationParent, created, null);

            /// <summary>
            /// Creates a failed operation result.
            /// </summary>
            public static CopyPreparationOutcome Failed(WebDavCopyResult failure) => new(null, null, false, failure);
        }

        private record CopyOperationOutcome(
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
            WebDavResolveResult sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
            if (!sourceResult.Found)
            {
                _logger.LogDebug("WebDAV COPY: Source not found: {Path}", request.SourcePath);
            }
            return sourceResult;
        }

        private async Task<WebDavParentResult> GetAndValidateDestinationParentAsync(WebDavCopyRequest request, CancellationToken ct)
        {
            WebDavParentResult destParentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.DestinationPath, ct);
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
            WebDavResolveResult destExists = await _pathResolver.ResolveMetadataAsync(request.UserId, request.DestinationPath, ct);
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
            Node sourceNode = await _dbContext.Nodes
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
            List<Node> childNodes = await _dbContext.Nodes
                .AsNoTracking()
                .Where(n => n.ParentId == sourceNodeId
                    && n.OwnerId == userId
                    && n.LayoutId == layoutId
                    && n.Type == sourceNode.Type)
                .ToListAsync(ct);

            foreach (Node? child in childNodes)
            {
                var (_, childAddedBytes) = await CopyNodeRecursivelyAsync(child.Id, newNode, child.Name, userId, layoutId, ct);
                addedBytes += childAddedBytes;
            }

            // Copy files
            List<NodeFile> childFiles = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(f => f.NodeId == sourceNodeId && f.OwnerId == userId)
                .ToListAsync(ct);

            foreach (NodeFile? file in childFiles)
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
}
