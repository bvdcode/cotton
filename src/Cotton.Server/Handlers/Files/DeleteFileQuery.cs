// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Represents a delete file query sent through the mediator pipeline.
    /// </summary>
    public class DeleteFileQuery(Guid userId, Guid nodeFileId, bool skipTrash, string? expectedETag = null) : IRequest
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;

        /// <summary>
        /// Gets the file entry identifier.
        /// </summary>
        public Guid NodeFileId { get; } = nodeFileId;

        /// <summary>
        /// Gets whether deletion bypasses trash and permanently removes the resource.
        /// </summary>
        public bool SkipTrash { get; } = skipTrash;

        /// <summary>
        /// Gets the optional expected file content ETag.
        /// </summary>
        public string? ExpectedETag { get; } = expectedETag;
    }

    /// <summary>
    /// Handles delete file queries in the mediator pipeline.
    /// </summary>
    public class DeleteFileQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ILayoutNavigator _navigator,
        ILogger<DeleteFileQueryHandler> _logger,
        UserStorageQuotaService _quota,
        ISyncChangeRecorder _syncChanges,
        ILayoutMutationGate _layoutGate,
        FileVersionService _versions)
            : IRequestHandler<DeleteFileQuery>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task Handle(DeleteFileQuery request, CancellationToken ct)
        {
            NodeFile nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .FirstOrDefaultAsync(x => x.Id == request.NodeFileId
                    && x.OwnerId == request.UserId, cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(FileManifest));
            EnsureETagPrecondition(request, nodeFile);

            if (request.SkipTrash)
            {
                if (FileVersionService.IsHistoricalVersion(nodeFile))
                {
                    await _versions.DeleteHistoricalVersionAsync(request.UserId, nodeFile.Id, ct);
                    return;
                }

                await DeletePermanentlyAsync(request, nodeFile, ct);
            }
            else
            {
                await MoveToTrashAsync(request, nodeFile, ct);
            }
        }

        private async Task DeletePermanentlyAsync(DeleteFileQuery command, NodeFile nodeFile, CancellationToken ct)
        {
            await using IDbContextTransaction? tx = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            long removedVersionBytes = await _versions.DeleteLineageVersionsAsync(command.UserId, nodeFile.Id, ct);
            await _dbContext.DownloadTokens
                .Where(t => t.CreatedByUserId == command.UserId && t.NodeFileId == nodeFile.Id)
                .ExecuteDeleteAsync(ct);
            long removedBytes = nodeFile.FileManifest.SizeBytes + removedVersionBytes;
            if (nodeFile.Node.Type == NodeType.Default)
            {
                _syncChanges.StageFileChange(SyncChangeKind.FileDeleted, nodeFile, nodeFile.Node.LayoutId);
            }
            _dbContext.NodeFiles.Remove(nodeFile);
            await _dbContext.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            _quota.RecordLogicalBytesRemoved(command.UserId, removedBytes);
            _logger.LogInformation("User {UserId} permanently deleted file {NodeFileId}.",
                command.UserId, command.NodeFileId);
        }

        private async Task MoveToTrashAsync(DeleteFileQuery command, NodeFile nodeFile, CancellationToken ct)
        {
            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(nodeFile.Node.LayoutId, ct);
            await using IDbContextTransaction? tx = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            nodeFile = await _dbContext.NodeFiles
                    .Include(x => x.Node)
                    .Include(x => x.FileManifest)
                    .Include(x => x.DownloadTokens)
                    .Where(x => x.Id == command.NodeFileId && x.OwnerId == command.UserId)
                    .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException(nameof(FileManifest));

            if (nodeFile.Node.Type != NodeType.Default)
            {
                throw new EntityNotFoundException(nameof(FileManifest));
            }

            EnsureETagPrecondition(command, nodeFile);

            string? originalParentPath = await _navigator.GetNodePathFromRootAsync(
                command.UserId,
                nodeFile.NodeId,
                NodeType.Default,
                ct);
            if (originalParentPath is not null)
            {
                nodeFile.Metadata = TrashRestoreCoordinator.SetOriginalParentPath(
                    nodeFile.Metadata,
                    originalParentPath);
            }

            Node trashItem = await _layouts.CreateTrashItemAsync(command.UserId, ct);
            _syncChanges.StageFileChange(SyncChangeKind.FileDeleted, nodeFile, nodeFile.Node.LayoutId);
            nodeFile.NodeId = trashItem.Id;
            foreach (DownloadToken share in nodeFile.DownloadTokens)
            {
                if (!share.ExpiresAt.HasValue || share.ExpiresAt.Value > DateTime.UtcNow)
                {
                    share.ExpiresAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            _logger.LogInformation("User {UserId} deleted file {NodeFileId} to trash.",
                command.UserId, command.NodeFileId);
        }

        private static void EnsureETagPrecondition(DeleteFileQuery request, NodeFile nodeFile)
        {
            if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, nodeFile))
            {
                throw new FilePreconditionFailedException<NodeFile>("File content changed before delete.");
            }
        }
    }
}
