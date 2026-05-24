// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

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
using Npgsql;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Represents a move node command sent through the mediator pipeline.
    /// </summary>
    public class MoveNodeCommand : IRequest<NodeDto>
    {
        /// <summary>
        /// Gets or sets the node identifier.
        /// </summary>
        public Guid NodeId { get; set; }
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
    /// Handles move node commands in the mediator pipeline.
    /// </summary>
    public class MoveNodeCommandHandler(
        CottonDbContext _dbContext,
        IEventNotificationService _eventNotification,
        ILogger<MoveNodeCommandHandler> _logger)
        : IRequestHandler<MoveNodeCommand, NodeDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<NodeDto> Handle(MoveNodeCommand request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);
            Guid sourceLayoutId = await GetSourceLayoutIdOrThrowAsync(request, cancellationToken);

            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, sourceLayoutId, cancellationToken);

            var node = await LoadSourceNodeOrThrowAsync(request, cancellationToken);
            ValidateSourceNode(node);
            if (node.ParentId == request.ParentId)
            {
                return node.Adapt<NodeDto>();
            }

            var targetParent = await LoadTargetParentOrThrowAsync(request, cancellationToken);
            await ValidateTargetParentAsync(request, node, targetParent, cancellationToken);

            Guid oldParentId = node.ParentId!.Value;
            await MoveNodeAsync(node, targetParent, cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await NotifyMoveAsync(node.Id, oldParentId, cancellationToken);
            return node.Adapt<NodeDto>();
        }

        private static void ValidateRequest(MoveNodeCommand request)
        {
            if (request.ParentId == Guid.Empty)
            {
                throw new BadRequestException<Node>("Target parent id is required.");
            }

            if (request.ParentId == request.NodeId)
            {
                throw new BadRequestException<Node>("Cannot move a node into itself.");
            }
        }

        private async Task<Guid> GetSourceLayoutIdOrThrowAsync(MoveNodeCommand request, CancellationToken ct)
        {
            return await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .Select(x => (Guid?)x.LayoutId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<Node>();
        }

        private async Task<Node> LoadSourceNodeOrThrowAsync(MoveNodeCommand request, CancellationToken ct)
        {
            return await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<Node>();
        }

        private static void ValidateSourceNode(Node node)
        {
            if (node.Type != NodeType.Default)
            {
                throw new EntityNotFoundException<Node>();
            }

            if (node.ParentId is null)
            {
                throw new AccessDeniedException<Node>("Cannot move the root node.");
            }
        }

        private async Task<Node> LoadTargetParentOrThrowAsync(MoveNodeCommand request, CancellationToken ct)
        {
            return await _dbContext.Nodes
                .Where(x => x.Id == request.ParentId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<Node>();
        }

        private async Task ValidateTargetParentAsync(
            MoveNodeCommand request,
            Node node,
            Node targetParent,
            CancellationToken ct)
        {
            EnsureCompatibleTargetParent(node, targetParent);
            if (await IsDescendantAsync(targetParent.Id, node.Id, request.UserId, ct))
            {
                throw new BadRequestException<Node>("Cannot move a folder into its descendant.");
            }

            await EnsureNoSiblingCollisionAsync(targetParent.Id, request.UserId, node.NameKey, node.Type, node.Id, ct);
        }

        private static void EnsureCompatibleTargetParent(Node node, Node targetParent)
        {
            if (targetParent.LayoutId != node.LayoutId)
            {
                throw new BadRequestException<Node>("Cannot move a node across layouts.");
            }

            if (targetParent.Type != node.Type)
            {
                throw new BadRequestException<Node>("Target parent has incompatible node type.");
            }
        }

        private async Task MoveNodeAsync(Node node, Node targetParent, CancellationToken ct)
        {
            node.SetParent(targetParent);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DuplicateException(node.NameKey);
            }
        }

        private async Task<bool> IsDescendantAsync(Guid candidateChildId, Guid possibleAncestorId, Guid userId, CancellationToken ct)
        {
            // Walks parent pointers upward from `candidateChildId`. If we encounter `possibleAncestorId`
            // before reaching the root, the candidate is a descendant of the ancestor.
            // Scoped to userId so a foreign tree can't pollute the walk.
            HashSet<Guid> visited = [];
            Guid? currentId = candidateChildId;
            while (currentId.HasValue)
            {
                if (!visited.Add(currentId.Value))
                {
                    throw new BadRequestException<Node>("Folder hierarchy contains a cycle.");
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

        private async Task NotifyMoveAsync(Guid nodeId, Guid oldParentId, CancellationToken ct)
        {
            // Best-effort: a notification failure must not turn an already-committed move into a failed response.
            try
            {
                await _eventNotification.NotifyNodeMovedAsync(nodeId, oldParentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Move notification failed for node {NodeId}", nodeId);
            }
        }
    }
}
