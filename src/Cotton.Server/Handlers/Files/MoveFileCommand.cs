// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Represents a move file command sent through the mediator pipeline.
    /// </summary>
    public class MoveFileCommand : IRequest<NodeFileManifestDto>
    {
        /// <summary>
        /// Gets or sets the file entry identifier.
        /// </summary>
        public Guid NodeFileId { get; set; }

        /// <summary>
        /// Gets or sets the parent folder identifier.
        /// </summary>
        public Guid ParentId { get; set; }

        /// <summary>
        /// Gets or sets the owning user identifier.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the optional expected file content ETag.
        /// </summary>
        public string? ExpectedETag { get; set; }
    }

    /// <summary>
    /// Handles move file commands in the mediator pipeline.
    /// </summary>
    public class MoveFileCommandHandler(
        CottonDbContext _dbContext,
        ISyncChangeRecorder _syncChanges,
        IEventNotificationService _eventNotification,
        ILayoutMutationGate _layoutGate,
        ILogger<MoveFileCommandHandler> _logger)
        : IRequestHandler<MoveFileCommand, NodeFileManifestDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<NodeFileManifestDto> Handle(MoveFileCommand request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);
            Guid sourceLayoutId = await GetSourceLayoutIdAsync(request, cancellationToken);

            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(sourceLayoutId, cancellationToken);
            await using IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            NodeFile nodeFile = await GetMovableFileAsync(request, cancellationToken);
            ValidateMovableFile(request, nodeFile);

            Guid? oldParentId = await MoveFileToTargetParentAsync(request, nodeFile, sourceLayoutId, cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await NotifyMoveIfNeededAsync(nodeFile.Id, oldParentId, cancellationToken);
            return nodeFile.Adapt<NodeFileManifestDto>();
        }

        private static void ValidateRequest(MoveFileCommand request)
        {
            if (request.ParentId == Guid.Empty)
            {
                throw new BadRequestException<NodeFile>("Target parent id is required.");
            }
        }

        private async Task<Guid> GetSourceLayoutIdAsync(MoveFileCommand request, CancellationToken cancellationToken)
        {
            return await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<NodeFile>();
        }

        private async Task<NodeFile> GetMovableFileAsync(MoveFileCommand request, CancellationToken cancellationToken)
        {
            NodeFile nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<NodeFile>();

            return nodeFile;
        }

        private static void ValidateMovableFile(MoveFileCommand request, NodeFile nodeFile)
        {
            if (nodeFile.Node.Type != NodeType.Default)
            {
                throw new EntityNotFoundException<NodeFile>();
            }

            if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, nodeFile))
            {
                throw new FilePreconditionFailedException<NodeFile>("File content changed before move.");
            }
        }

        private async Task<Guid?> MoveFileToTargetParentAsync(
            MoveFileCommand request,
            NodeFile nodeFile,
            Guid sourceLayoutId,
            CancellationToken cancellationToken)
        {
            if (nodeFile.NodeId == request.ParentId)
            {
                return null;
            }

            Node targetParent = await GetTargetParentAsync(request, cancellationToken);
            ValidateTargetParent(nodeFile, targetParent);

            await EnsureNoSiblingCollisionAsync(targetParent.Id, request.UserId, nodeFile.NameKey, nodeFile.Id, cancellationToken);

            Guid oldParentId = nodeFile.NodeId;
            nodeFile.NodeId = targetParent.Id;
            _syncChanges.StageFileChange(SyncChangeKind.FileMoved, nodeFile, sourceLayoutId, oldParentId);
            await SaveMovedFileAsync(nodeFile, cancellationToken);
            return oldParentId;
        }

        private async Task<Node> GetTargetParentAsync(MoveFileCommand request, CancellationToken cancellationToken)
        {
            return await _dbContext.Nodes
                .Where(x => x.Id == request.ParentId
                    && x.OwnerId == request.UserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<Node>();
        }

        private static void ValidateTargetParent(NodeFile nodeFile, Node targetParent)
        {
            if (targetParent.LayoutId != nodeFile.Node.LayoutId)
            {
                throw new BadRequestException<Node>("Cannot move a file across layouts.");
            }
        }

        private async Task SaveMovedFileAsync(NodeFile nodeFile, CancellationToken cancellationToken)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DuplicateException(nodeFile.NameKey);
            }
        }

        private async Task EnsureNoSiblingCollisionAsync(
            Guid targetParentId,
            Guid userId,
            string nameKey,
            Guid movingFileId,
            CancellationToken ct)
        {
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x =>
                    x.NodeId == targetParentId &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.Id != movingFileId,
                    ct);
            if (fileExists)
            {
                throw new DuplicateException(nameKey);
            }

            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == targetParentId &&
                    x.OwnerId == userId &&
                    x.Type == NodeType.Default &&
                    x.NameKey == nameKey,
                    ct);
            if (nodeExists)
            {
                throw new DuplicateException(nameKey);
            }
        }

        private async Task NotifyMoveIfNeededAsync(Guid nodeFileId, Guid? oldParentId, CancellationToken ct)
        {
            if (!oldParentId.HasValue)
            {
                return;
            }

            await NotifyMoveAsync(nodeFileId, oldParentId.Value, ct);
        }

        private async Task NotifyMoveAsync(Guid nodeFileId, Guid oldParentId, CancellationToken ct)
        {
            // Best-effort: a notification failure must not turn an already-committed move into a failed response.
            try
            {
                await _eventNotification.NotifyFileMovedAsync(nodeFileId, oldParentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Move notification failed for file {NodeFileId}", nodeFileId);
            }
        }
    }
}
