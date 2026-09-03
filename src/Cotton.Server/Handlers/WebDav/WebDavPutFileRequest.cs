// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Quartz;

namespace Cotton.Server.Handlers.WebDav
{
    public record WebDavPutFileRequest(
        Guid UserId,
        string Path,
        Stream Content,
        string? ContentType,
        bool Overwrite = true,
        long? ContentLength = null) : IRequest<WebDavPutFileResult>;

    public class WebDavPutFileRequestHandler(
        CottonDbContext _dbContext,
        ISchedulerFactory _scheduler,
        FileVersionService _versions,
        WebDavPutContentReader _contentReader,
        IWebDavPathResolver _pathResolver,
        FileManifestService _fileManifestService,
        UserStorageQuotaService _quota,
        IEventNotificationService _eventNotification,
        ISyncChangeRecorder _syncChanges,
        ILayoutMutationGate _layoutGate,
        ILogger<WebDavPutFileRequestHandler> _logger)
        : IRequestHandler<WebDavPutFileRequest, WebDavPutFileResult>
    {
        private record PutTarget(
            WebDavResolveResult Existing,
            WebDavParentResult Parent,
            string ResourceName,
            string NameKey,
            bool Created);

        private static WebDavPutFileResult Fail(WebDavPutFileError error) => new(false, false, error);

        public async Task<WebDavPutFileResult> Handle(WebDavPutFileRequest request, CancellationToken ct)
        {
            var (target, targetError) = await TryResolveAndValidateTargetAsync(request, ct);
            if (targetError is not null)
            {
                return targetError;
            }

            WebDavPutFileResult? quotaPreflightError = await TryPreflightKnownLengthQuotaAsync(request, target!, ct);
            if (quotaPreflightError is not null)
            {
                return quotaPreflightError;
            }

            var (content, contentError) = await _contentReader.ReadAsync(request, ct);
            if (contentError is not null)
            {
                return contentError;
            }

            string contentType = FileContentTypeResolver.Resolve(target!.ResourceName, request.ContentType);
            FileManifest fileManifest = await GetOrCreateFileManifestAsync(
                chunks: content!.Chunks,
                fileHash: content.FileHash,
                userId: request.UserId,
                resourceName: target.ResourceName,
                contentType: contentType,
                ct);

            // Re-resolve the target inside the transaction: the original path result can be stale after a long upload stream.
            Guid expectedLayoutId = target.Parent.ParentNode!.LayoutId;
            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(expectedLayoutId, ct);
            (PutTarget? finalTarget, NodeFile? resultNodeFile, WebDavPutFileResult? commitError) =
                await CommitPutAsync(request, fileManifest.Id, expectedLayoutId, ct);
            if (commitError is not null)
            {
                return commitError;
            }

            await NotifyPutCompletedAsync(
                request,
                created: finalTarget!.Created,
                chunkCount: content.Chunks.Count,
                nodeFileId: resultNodeFile!.Id,
                ct);
            return new WebDavPutFileResult(true, finalTarget.Created, null, resultNodeFile.Id);
        }

        private async Task<(PutTarget? Target, NodeFile? NodeFile, WebDavPutFileResult? Error)> CommitPutAsync(
            WebDavPutFileRequest request,
            Guid fileManifestId,
            Guid expectedLayoutId,
            CancellationToken ct)
        {
            await using (IAsyncDisposable quotaGate = await _quota.EnterMutationAsync(request.UserId, ct))
            await using (IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync(ct))
            {
                (PutTarget? refreshedTarget, WebDavPutFileResult? refreshedTargetError) = await TryResolveAndValidateTargetAsync(request, ct);
                if (refreshedTargetError is not null)
                {
                    return (null, null, refreshedTargetError);
                }

                PutTarget finalTarget = refreshedTarget!;
                if (finalTarget.Parent.ParentNode!.LayoutId != expectedLayoutId)
                {
                    _logger.LogDebug("WebDAV PUT: Parent layout changed before commit: {Path}", request.Path);
                    return (null, null, Fail(WebDavPutFileError.ParentNotFound));
                }

                (WebDavPutFileResult? quotaError, long addedBytes) = await TryEnsureQuotaAsync(request, finalTarget, fileManifestId, ct);
                if (quotaError is not null)
                {
                    return (null, null, quotaError);
                }

                (NodeFile resultNodeFile, FileVersionCaptureResult capture) = await UpsertNodeFileAsync(
                    request,
                    finalTarget,
                    fileManifestId,
                    ct);
                _syncChanges.StageFileChange(
                    finalTarget.Created ? SyncChangeKind.FileCreated : SyncChangeKind.FileContentUpdated,
                    resultNodeFile,
                    finalTarget.Parent.ParentNode.LayoutId);
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                _quota.RecordLogicalBytesAdded(request.UserId, addedBytes);
                if (capture.RemovedBytes > 0)
                {
                    _quota.RecordLogicalBytesRemoved(request.UserId, capture.RemovedBytes);
                }

                return (finalTarget, resultNodeFile, null);
            }
        }

        private async Task<(PutTarget? Target, WebDavPutFileResult? Error)> TryResolveAndValidateTargetAsync(WebDavPutFileRequest request, CancellationToken ct)
        {
            WebDavResolveResult existing = await ResolveExistingAsync(request, ct);
            WebDavParentResult parentResult = await ResolveParentAsync(request, ct);

            WebDavPutFileResult? preValidationFailure = TryGetExistingValidationFailure(existing)
                ?? TryGetParentValidationFailure(parentResult);
            if (preValidationFailure is not null)
            {
                return (null, preValidationFailure);
            }

            string resourceName = parentResult.ResourceName!;
            WebDavPutFileResult? nameFailure = TryGetNameValidationFailure(resourceName);
            if (nameFailure is not null)
            {
                return (null, nameFailure);
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(resourceName);

            WebDavPutFileResult? validationFailure = await TryGetFolderConflictFailureAsync(
                    userId: request.UserId,
                    parentNodeId: parentResult.ParentNode!.Id,
                    nameKey: nameKey,
                    layoutId: parentResult.ParentNode.LayoutId,
                    ct)
                ?? TryGetOverwriteValidationFailure(existing, request.Overwrite);

            if (validationFailure is not null)
            {
                return (null, validationFailure);
            }

            return (new PutTarget(existing, parentResult, resourceName, nameKey, Created: !existing.Found), null);
        }

        private Task<WebDavResolveResult> ResolveExistingAsync(WebDavPutFileRequest request, CancellationToken ct)
        {
            return _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);
        }

        private Task<WebDavParentResult> ResolveParentAsync(WebDavPutFileRequest request, CancellationToken ct)
        {
            return _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        }

        private static WebDavPutFileResult? TryGetExistingValidationFailure(WebDavResolveResult existing)
        {
            if (existing.Found && existing.IsCollection)
            {
                return Fail(WebDavPutFileError.IsCollection);
            }

            return null;
        }

        private static WebDavPutFileResult? TryGetParentValidationFailure(WebDavParentResult parentResult)
        {
            if (!parentResult.Found || parentResult.ParentNode is null || parentResult.ResourceName is null)
            {
                return Fail(WebDavPutFileError.ParentNotFound);
            }

            return null;
        }

        private static WebDavPutFileResult? TryGetNameValidationFailure(string resourceName)
        {
            if (!NameValidator.TryNormalizeAndValidate(resourceName, out _, out _))
            {
                return Fail(WebDavPutFileError.InvalidName);
            }

            return null;
        }

        private async Task<WebDavPutFileResult?> TryGetFolderConflictFailureAsync(
            Guid userId,
            Guid parentNodeId,
            string nameKey,
            Guid layoutId,
            CancellationToken ct)
        {
            bool folderExists = await _dbContext.Nodes
                .AnyAsync(n => n.ParentId == parentNodeId
                    && n.OwnerId == userId
                    && n.NameKey == nameKey
                    && n.LayoutId == layoutId
                    && n.Type == WebDavPathResolver.DefaultNodeType, ct);

            return folderExists ? Fail(WebDavPutFileError.Conflict) : null;
        }

        private static WebDavPutFileResult? TryGetOverwriteValidationFailure(WebDavResolveResult existing, bool overwrite)
        {
            if (existing.Found && existing.NodeFile is not null && !overwrite)
            {
                return Fail(WebDavPutFileError.PreconditionFailed);
            }

            return null;
        }

        private async Task<FileManifest> GetOrCreateFileManifestAsync(
            List<Chunk> chunks,
            byte[] fileHash,
            Guid userId,
            string resourceName,
            string contentType,
            CancellationToken ct)
        {
            FileManifest? fileManifest = await _fileManifestService.GetReusableOwnedManifestAsync(fileHash, userId, cancellationToken: ct);

            if (fileManifest is not null)
            {
                await _fileManifestService.ClearGcSchedulesForManifestReferencesAsync(fileManifest.Id, ct);
            }
            else
            {
                fileManifest = await _fileManifestService.CreateNewFileManifestAsync(
                    chunks,
                    resourceName,
                    contentType,
                    fileHash,
                    userId,
                    cancellationToken: ct);
            }

            return fileManifest;
        }

        private async Task<WebDavPutFileResult?> TryPreflightKnownLengthQuotaAsync(
            WebDavPutFileRequest request,
            PutTarget target,
            CancellationToken ct)
        {
            if (request.ContentLength is not long contentLength || contentLength < 0)
            {
                return null;
            }

            try
            {
                if (target.Existing.Found)
                {
                    return null;
                }

                await _quota.EnsureCanAddKnownFileSizeAsync(request.UserId, contentLength, ct);

                return null;
            }
            catch (StorageQuotaExceededException<User>)
            {
                return Fail(WebDavPutFileError.QuotaExceeded);
            }
        }

        private async Task<(WebDavPutFileResult? Error, long AddedBytes)> TryEnsureQuotaAsync(WebDavPutFileRequest request, PutTarget target, Guid fileManifestId, CancellationToken ct)
        {
            try
            {
                if (target.Existing.Found && target.Existing.NodeFile is not null)
                {
                    long changedBytes = await _quota.EnsureCanChangeFileManifestAsync(request.UserId, target.Existing.NodeFile.Id, fileManifestId, ct);
                    return (null, changedBytes);
                }

                long addedBytes = await _quota.EnsureCanAddFileReferenceAsync(request.UserId, fileManifestId, ct);
                return (null, addedBytes);
            }
            catch (StorageQuotaExceededException<User>)
            {
                return (Fail(WebDavPutFileError.QuotaExceeded), 0);
            }
        }

        private async Task<(NodeFile NodeFile, FileVersionCaptureResult Capture)> UpsertNodeFileAsync(WebDavPutFileRequest request, PutTarget target, Guid fileManifestId, CancellationToken ct)
        {
            if (target.Existing.Found && target.Existing.NodeFile is not null)
            {
                NodeFile nodeFile = await _dbContext.NodeFiles
                    .FirstAsync(f => f.Id == target.Existing.NodeFile.Id, ct);

                FileManifest? previousManifest = await _dbContext.FileManifests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == nodeFile.FileManifestId, ct);

                if (previousManifest?.SizeBytes == 0)
                {
                    nodeFile.FileManifestId = fileManifestId;
                    return (nodeFile, FileVersionCaptureResult.Empty);
                }

                FileVersionCaptureResult capture = await _versions.CaptureAndUpdateManifestAsync(
                    nodeFile,
                    fileManifestId,
                    request.UserId,
                    ct);
                return (nodeFile, capture);
            }

            NodeFile createdNodeFile = new NodeFile
            {
                OwnerId = request.UserId,
                NodeId = target.Parent.ParentNode!.Id,
                FileManifestId = fileManifestId,
            };
            createdNodeFile.SetName(target.ResourceName);

            await _dbContext.NodeFiles.AddAsync(createdNodeFile, ct);
            await _dbContext.SaveChangesAsync(ct);

            createdNodeFile.OriginalNodeFileId = createdNodeFile.Id;
            return (createdNodeFile, FileVersionCaptureResult.Empty);
        }

        private async Task NotifyPutCompletedAsync(
            WebDavPutFileRequest request,
            bool created,
            int chunkCount,
            Guid nodeFileId,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "WebDAV PUT: {Action} file {Path} ({ChunkCount} chunks) for user {UserId}",
                created ? "Created" : "Updated",
                request.Path,
                chunkCount,
                request.UserId);

            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            await _scheduler.TriggerJobAsync<ExtractFileMetadataJob>();

            if (created)
            {
                await _eventNotification.NotifyFileCreatedAsync(nodeFileId, ct);
            }
            else
            {
                await _eventNotification.NotifyFileUpdatedAsync(nodeFileId, ct);
            }
        }

    }
}
