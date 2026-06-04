// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    }

    /// <summary>
    /// Handles move file commands in the mediator pipeline.
    /// </summary>
    public class MoveFileCommandHandler(
        CottonDbContext _dbContext,
        ISyncChangeRecorder _syncChanges,
        IEventNotificationService _eventNotification,
        ILogger<MoveFileCommandHandler> _logger)
        : IRequestHandler<MoveFileCommand, NodeFileManifestDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<NodeFileManifestDto> Handle(MoveFileCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentId == Guid.Empty)
            {
                throw new BadRequestException<NodeFile>("Target parent id is required.");
            }

            // The cross-table namespace (file vs folder with the same NameKey under
            // the same parent) is NOT protected by a single unique index. Without a
            // serialization point, a concurrent file move + folder move can both
            // pass their pre-checks and commit a same-name cross-type duplicate.
            // We take the same per-layout advisory lock that MoveNodeCommand uses.
            var sourceLayoutId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<NodeFile>();

            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, sourceLayoutId, cancellationToken);

            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<NodeFile>();

            if (nodeFile.Node.Type != NodeType.Default)
            {
                throw new EntityNotFoundException<NodeFile>();
            }

            if (nodeFile.NodeId == request.ParentId)
            {
                await tx.CommitAsync(cancellationToken);
                return nodeFile.Adapt<NodeFileManifestDto>();
            }

            var targetParent = await _dbContext.Nodes
                .Where(x => x.Id == request.ParentId
                    && x.OwnerId == request.UserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<Node>();

            if (targetParent.LayoutId != nodeFile.Node.LayoutId)
            {
                throw new BadRequestException<Node>("Cannot move a file across layouts.");
            }

            await EnsureNoSiblingCollisionAsync(targetParent.Id, request.UserId, nodeFile.NameKey, nodeFile.Id, cancellationToken);

            Guid oldParentId = nodeFile.NodeId;
            nodeFile.NodeId = targetParent.Id;
            _syncChanges.StageFileChange(SyncChangeKind.FileMoved, nodeFile, sourceLayoutId, oldParentId);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DuplicateException(nodeFile.NameKey);
            }

            await tx.CommitAsync(cancellationToken);

            await NotifyMoveAsync(nodeFile.Id, oldParentId, cancellationToken);
            return nodeFile.Adapt<NodeFileManifestDto>();
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
