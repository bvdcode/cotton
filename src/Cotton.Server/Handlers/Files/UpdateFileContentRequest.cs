// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Services;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Updates a file from already uploaded chunks.
    /// </summary>
    public record UpdateFileContentRequest(
        Guid UserId,
        Guid NodeFileId,
        string Name,
        string ContentType,
        string Hash,
        string[] ChunkHashes,
        Dictionary<string, string>? Metadata,
        string? ExpectedETag) : IRequest<UpdateFileContentResult>;

    /// <summary>
    /// Handles file content updates.
    /// </summary>
    public class UpdateFileContentRequestHandler(
        CottonDbContext _dbContext,
        ISyncChangeRecorder _syncChanges,
        FileManifestService _fileManifestService,
        FileVersionService _versions,
        UserStorageQuotaService _quota,
        ILayoutMutationGate _layoutGate)
        : IRequestHandler<UpdateFileContentRequest, UpdateFileContentResult>
    {
        /// <summary>
        /// Updates the requested file while preserving quota and version invariants.
        /// </summary>
        public async Task<UpdateFileContentResult> Handle(
            UpdateFileContentRequest request,
            CancellationToken ct)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(
                request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return Failure(UpdateFileContentStatus.InvalidName, errorMessage);
            }

            Guid? layoutId = await GetOwnedFileLayoutIdAsync(request, ct);
            if (layoutId is null)
            {
                return Failure(
                    UpdateFileContentStatus.FileNotFound,
                    "Node file not found.");
            }

            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            FileManifest newFile = await ResolveUpdateManifestAsync(
                request,
                proposedHash,
                ct);

            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(
                layoutId.Value,
                ct);
            await using IAsyncDisposable quotaGate = await _quota.EnterMutationAsync(
                request.UserId,
                ct);
            await using IDbContextTransaction tx = await _dbContext.Database
                .BeginTransactionAsync(ct);

            NodeFile? nodeFile = await LoadEditableNodeFileAsync(request, ct);
            if (nodeFile is null)
            {
                return Failure(
                    UpdateFileContentStatus.FileNotFound,
                    "Node file not found.");
            }

            if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, nodeFile))
            {
                throw new FilePreconditionFailedException<NodeFile>(
                    "File content changed before update.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(normalizedName);
            string? conflictMessage = await FindNameConflictAsync(
                nodeFile,
                request,
                nameKey,
                ct);
            if (conflictMessage is not null)
            {
                return Failure(
                    UpdateFileContentStatus.NameConflict,
                    conflictMessage);
            }

            long addedBytes = await _quota.EnsureCanChangeFileManifestAsync(
                request.UserId,
                nodeFile.Id,
                newFile.Id,
                ct);
            FileVersionCaptureResult capture = await ApplyUpdatedContentAsync(
                nodeFile,
                newFile,
                proposedHash,
                normalizedName,
                request.Metadata,
                request.UserId,
                ct);

            _syncChanges.StageFileChange(
                SyncChangeKind.FileContentUpdated,
                nodeFile,
                layoutId.Value);
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _quota.RecordLogicalBytesAdded(request.UserId, addedBytes);
            if (capture.RemovedBytes > 0)
            {
                _quota.RecordLogicalBytesRemoved(
                    request.UserId,
                    capture.RemovedBytes);
            }

            return new UpdateFileContentResult(
                UpdateFileContentStatus.Updated,
                nodeFile.Adapt<NodeFileManifestDto>());
        }

        private async Task<Guid?> GetOwnedFileLayoutIdAsync(
            UpdateFileContentRequest request,
            CancellationToken ct)
        {
            return await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == request.NodeFileId
                    && x.OwnerId == request.UserId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync(ct);
        }

        private async Task<FileManifest> ResolveUpdateManifestAsync(
            UpdateFileContentRequest request,
            byte[] proposedHash,
            CancellationToken ct)
        {
            List<Chunk> chunks = await _fileManifestService.GetChunksAsync(
                request.ChunkHashes,
                request.UserId,
                ct);
            return await _fileManifestService.GetReusableOwnedManifestAsync(
                proposedHash,
                request.UserId,
                cancellationToken: ct)
                ?? await _fileManifestService.CreateNewFileManifestAsync(
                    chunks,
                    request.Name,
                    request.ContentType,
                    proposedHash,
                    request.UserId,
                    cancellationToken: ct);
        }

        private async Task<NodeFile?> LoadEditableNodeFileAsync(
            UpdateFileContentRequest request,
            CancellationToken ct)
        {
            return await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == request.NodeFileId
                    && x.OwnerId == request.UserId
                    && x.Node.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);
        }

        private async Task<string?> FindNameConflictAsync(
            NodeFile nodeFile,
            UpdateFileContentRequest request,
            string nameKey,
            CancellationToken ct)
        {
            if (string.Equals(nodeFile.NameKey, nameKey, StringComparison.Ordinal))
            {
                return null;
            }

            bool fileExists = await _dbContext.NodeFiles.AnyAsync(
                x => x.NodeId == nodeFile.NodeId
                    && x.OwnerId == request.UserId
                    && x.NameKey == nameKey
                    && x.Id != request.NodeFileId,
                ct);
            if (fileExists)
            {
                return "A file with the same name key already exists in this folder: " + nameKey;
            }

            bool nodeExists = await _dbContext.Nodes.AnyAsync(
                x => x.ParentId == nodeFile.NodeId
                    && x.OwnerId == request.UserId
                    && x.Type == nodeFile.Node.Type
                    && x.NameKey == nameKey,
                ct);
            return nodeExists
                ? "A folder with the same name key already exists in this folder: " + nameKey
                : null;
        }

        private async Task<FileVersionCaptureResult> ApplyUpdatedContentAsync(
            NodeFile nodeFile,
            FileManifest newFile,
            byte[] proposedHash,
            string normalizedName,
            Dictionary<string, string>? metadata,
            Guid userId,
            CancellationToken ct)
        {
            FileVersionCaptureResult capture = FileVersionCaptureResult.Empty;
            if (!nodeFile.FileManifest.ProposedContentHash.SequenceEqual(proposedHash))
            {
                capture = await _versions.CaptureAndUpdateManifestAsync(
                    nodeFile,
                    newFile.Id,
                    userId,
                    ct);
                nodeFile.FileManifest = newFile;
            }

            nodeFile.SetName(normalizedName);
            if (metadata is not null)
            {
                nodeFile.Metadata = metadata.Count > 0
                    ? new Dictionary<string, string>(metadata)
                    : [];
            }

            return capture;
        }

        private static UpdateFileContentResult Failure(
            UpdateFileContentStatus status,
            string? error) => new(status, Error: error);
    }
}
