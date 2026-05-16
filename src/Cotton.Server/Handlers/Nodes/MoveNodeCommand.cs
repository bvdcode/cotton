// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.Handlers.Nodes
{
    public class MoveNodeCommand : IRequest<NodeDto>
    {
        public Guid NodeId { get; set; }
        public Guid ParentId { get; set; }
        public Guid UserId { get; set; }
    }

    public class MoveNodeCommandHandler(
        CottonDbContext _dbContext,
        IEventNotificationService _eventNotification,
        ILogger<MoveNodeCommandHandler> _logger)
        : IRequestHandler<MoveNodeCommand, NodeDto>
    {
        private const int MaxAncestorWalkDepth = 256;

        public async Task<NodeDto> Handle(MoveNodeCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentId == Guid.Empty)
            {
                throw new BadRequestException<Node>("Target parent id is required.");
            }

            if (request.ParentId == request.NodeId)
            {
                throw new BadRequestException<Node>("Cannot move a node into itself.");
            }

            var node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<Node>();

            if (node.ParentId is null)
            {
                throw new AccessDeniedException<Node>("Cannot move the root node.");
            }

            if (node.ParentId == request.ParentId)
            {
                return node.Adapt<NodeDto>();
            }

            var targetParent = await _dbContext.Nodes
                .Where(x => x.Id == request.ParentId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException<Node>();

            if (targetParent.LayoutId != node.LayoutId)
            {
                // A user can own multiple layouts; nodes from different layouts live in disjoint
                // namespaces (the unique index is keyed by LayoutId), so cross-layout moves would
                // either bypass that index or silently orphan the subtree's LayoutId.
                throw new BadRequestException<Node>("Cannot move a node across layouts.");
            }

            if (targetParent.Type != node.Type)
            {
                throw new BadRequestException<Node>("Target parent has incompatible node type.");
            }

            if (await IsDescendantAsync(targetParent.Id, node.Id, request.UserId, cancellationToken))
            {
                throw new BadRequestException<Node>("Cannot move a folder into its descendant.");
            }

            await EnsureNoSiblingCollisionAsync(targetParent.Id, request.UserId, node.NameKey, node.Type, node.Id, cancellationToken);

            node.ParentId = targetParent.Id;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // Unique index (LayoutId, ParentId, Type, NameKey) lost a race with a concurrent insert/move; surface as 409.
                throw new DuplicateException(node.NameKey);
            }

            await NotifyMoveAsync(node.Id, cancellationToken);
            return node.Adapt<NodeDto>();
        }

        private async Task<bool> IsDescendantAsync(Guid candidateChildId, Guid possibleAncestorId, Guid userId, CancellationToken ct)
        {
            // Walks parent pointers upward from `candidateChildId`. If we encounter `possibleAncestorId`
            // before reaching the root, the candidate is a descendant of the ancestor.
            // Scoped to userId so a foreign tree can't pollute the walk.
            int depth = 0;
            Guid? currentId = candidateChildId;
            while (currentId.HasValue)
            {
                if (depth++ >= MaxAncestorWalkDepth)
                {
                    return true;
                }

                if (currentId.Value == possibleAncestorId)
                {
                    return true;
                }

                currentId = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(n => n.Id == currentId.Value && n.OwnerId == userId)
                    .Select(n => n.ParentId)
                    .SingleOrDefaultAsync(ct);
            }

            return false;
        }

        private async Task EnsureNoSiblingCollisionAsync(
            Guid targetParentId,
            Guid userId,
            string nameKey,
            Cotton.Database.Models.Enums.NodeType nodeType,
            Guid movingNodeId,
            CancellationToken ct)
        {
            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == targetParentId &&
                    x.OwnerId == userId &&
                    x.Type == nodeType &&
                    x.NameKey == nameKey &&
                    x.Id != movingNodeId,
                    ct);
            if (nodeExists)
            {
                throw new DuplicateException(nameKey);
            }

            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x =>
                    x.NodeId == targetParentId &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey,
                    ct);
            if (fileExists)
            {
                throw new DuplicateException(nameKey);
            }
        }

        private async Task NotifyMoveAsync(Guid nodeId, CancellationToken ct)
        {
            // Best-effort: a notification failure must not turn an already-committed move into a failed response.
            // TODO: include oldParentId/newParentId in a NodeMovedEventDto so clients can invalidate both parents.
            try
            {
                await _eventNotification.NotifyNodeMovedAsync(nodeId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Move notification failed for node {NodeId}", nodeId);
            }
        }
    }
}
