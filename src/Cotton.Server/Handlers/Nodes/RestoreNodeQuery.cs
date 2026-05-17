// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.Handlers.Nodes
{
    public class RestoreNodeQuery(Guid userId, Guid nodeId, bool createMissingParents, bool overwrite)
        : IRequest<RestoreOutcomeDto>
    {
        public Guid UserId { get; } = userId;
        public Guid NodeId { get; } = nodeId;
        public bool CreateMissingParents { get; } = createMissingParents;
        public bool Overwrite { get; } = overwrite;
    }

    public class RestoreNodeQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        TrashRestoreCoordinator _restore,
        NodeSubtreeService _subtree,
        ILogger<RestoreNodeQueryHandler> _logger)
        : IRequestHandler<RestoreNodeQuery, RestoreOutcomeDto>
    {
        public async Task<RestoreOutcomeDto> Handle(RestoreNodeQuery request, CancellationToken ct)
        {
            Guid layoutId = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .Select(x => x.LayoutId)
                .SingleOrDefaultAsync(ct);
            if (layoutId == Guid.Empty)
            {
                throw new EntityNotFoundException<Node>();
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, layoutId, ct);

            var node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<Node>();

            if (node.Type != NodeType.Trash)
            {
                await tx.RollbackAsync(ct);
                return NotRestorable("Node is not in trash.");
            }

            var wrapper = node.ParentId.HasValue
                ? await _dbContext.Nodes
                    .Where(x => x.Id == node.ParentId.Value && x.OwnerId == request.UserId)
                    .SingleOrDefaultAsync(ct)
                : null;
            var trashRoot = await _layouts.GetUserTrashRootAsync(request.UserId);
            if (wrapper is null || wrapper.ParentId != trashRoot.Id)
            {
                await tx.RollbackAsync(ct);
                return NotRestorable("Item can only be restored from the top level of trash.");
            }

            string originalParentPath = TrashRestoreCoordinator.GetOriginalParentPath(node.Metadata);

            try
            {
                var resolution = await _restore.ResolveOrCreateParentAsync(
                    request.UserId,
                    originalParentPath,
                    request.CreateMissingParents,
                    ct);
                if (resolution.InvalidPathReason is not null)
                {
                    await tx.RollbackAsync(ct);
                    return NotRestorable(resolution.InvalidPathReason, originalParentPath);
                }

                var targetParent = resolution.Parent;
                if (targetParent is null)
                {
                    await tx.RollbackAsync(ct);
                    return new RestoreOutcomeDto
                    {
                        Status = RestoreStatus.ParentMissing,
                        OriginalParentPath = originalParentPath,
                        MissingPath = originalParentPath,
                    };
                }

                var conflict = await _restore.FindConflictAsync(request.UserId, targetParent.Id, node.NameKey, ct);
                if (conflict.HasValue)
                {
                    if (!request.Overwrite)
                    {
                        await tx.RollbackAsync(ct);
                        return new RestoreOutcomeDto
                        {
                            Status = RestoreStatus.Conflict,
                            OriginalParentPath = originalParentPath,
                            ConflictKind = conflict.Value.Kind,
                            ConflictName = conflict.Value.Name,
                        };
                    }

                    await _restore.SendConflictToTrashAsync(request.UserId, conflict.Value, ct);
                }

                node.ParentId = targetParent.Id;
                node.Metadata = TrashRestoreCoordinator.RemoveOriginalParentPath(node.Metadata);
                await _subtree.SetSubtreeTypeAsync(request.UserId, node.Id, NodeType.Default, ct);
                await _dbContext.SaveChangesAsync(ct);

                await _restore.DeleteWrapperIfEmptyAsync(request.UserId, wrapper, ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "User {UserId} restored node {NodeId} into parent {ParentId}.",
                    request.UserId,
                    node.Id,
                    targetParent.Id);

                return new RestoreOutcomeDto
                {
                    Status = RestoreStatus.Restored,
                    OriginalParentPath = originalParentPath,
                    RestoredNode = node.Adapt<NodeDto>(),
                };
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    ex,
                    "Concurrent unique violation while restoring node {NodeId} for user {UserId}.",
                    request.NodeId,
                    request.UserId);
                return new RestoreOutcomeDto
                {
                    Status = RestoreStatus.Conflict,
                    OriginalParentPath = originalParentPath,
                    ConflictKind = RestoreConflictKind.Folder,
                    ConflictName = node.Name,
                };
            }
        }

        private static RestoreOutcomeDto NotRestorable(string reason, string? originalParentPath = null) => new()
        {
            Status = RestoreStatus.NotRestorable,
            OriginalParentPath = originalParentPath,
            Reason = reason,
        };

        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
