// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public class MoveFileCommand : IRequest<NodeFileManifestDto>
    {
        public Guid NodeFileId { get; set; }
        public Guid ParentId { get; set; }
        public Guid UserId { get; set; }
    }

    public class MoveFileCommandHandler(
        CottonDbContext _dbContext,
        IEventNotificationService _eventNotification,
        ILogger<MoveFileCommandHandler> _logger)
        : IRequestHandler<MoveFileCommand, NodeFileManifestDto>
    {
        public async Task<NodeFileManifestDto> Handle(MoveFileCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentId == Guid.Empty)
            {
                throw new BadRequestException<NodeFile>("Target parent id is required.");
            }

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
                return nodeFile.Adapt<NodeFileManifestDto>();
            }

            var targetParent = await _dbContext.Nodes
                .Where(x => x.Id == request.ParentId
                    && x.OwnerId == request.UserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<Node>();

            await EnsureNoSiblingCollisionAsync(targetParent.Id, request.UserId, nodeFile.NameKey, nodeFile.Id, cancellationToken);

            nodeFile.NodeId = targetParent.Id;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Unique index (NodeId, NameKey) lost a race; surface as 409.
                throw new DuplicateException(nodeFile.NameKey);
            }

            await NotifyMoveAsync(nodeFile.Id, cancellationToken);
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

        private async Task NotifyMoveAsync(Guid nodeFileId, CancellationToken ct)
        {
            // Best-effort: a notification failure must not turn an already-committed move into a failed response.
            // TODO: include oldParentId/newParentId in a NodeFileMovedEventDto so clients can invalidate both parents.
            try
            {
                await _eventNotification.NotifyFileMovedAsync(nodeFileId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Move notification failed for file {NodeFileId}", nodeFileId);
            }
        }
    }
}
