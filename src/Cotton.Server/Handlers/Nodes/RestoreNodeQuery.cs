// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
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
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Represents a restore node query sent through the mediator pipeline.
    /// </summary>
    public class RestoreNodeQuery(Guid userId, Guid nodeId, bool createMissingParents, bool overwrite)
        : IRequest<RestoreOutcomeDto>
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the node identifier.
        /// </summary>
        public Guid NodeId { get; } = nodeId;
        /// <summary>
        /// Creates missing parents.
        /// </summary>
        public bool CreateMissingParents { get; } = createMissingParents;
        /// <summary>
        /// Gets whether restore should move an existing conflicting item to trash.
        /// </summary>
        public bool Overwrite { get; } = overwrite;
    }

    /// <summary>
    /// Handles restore node queries in the mediator pipeline.
    /// </summary>
    public class RestoreNodeQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        TrashRestoreCoordinator _restore,
        NodeSubtreeService _subtree,
        ISyncChangeRecorder _syncChanges,
        ILogger<RestoreNodeQueryHandler> _logger)
        : IRequestHandler<RestoreNodeQuery, RestoreOutcomeDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<RestoreOutcomeDto> Handle(RestoreNodeQuery request, CancellationToken ct)
        {
            Guid layoutId = await GetLayoutIdOrThrowAsync(request, ct);

            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, layoutId, ct);

            var node = await LoadNodeOrThrowAsync(request, ct);
            var wrapperOutcome = await ResolveTopLevelTrashWrapperAsync(request, node, ct);
            if (wrapperOutcome.Failure is not null)
            {
                await tx.RollbackAsync(ct);
                return wrapperOutcome.Failure;
            }

            string originalParentPath = TrashRestoreCoordinator.GetOriginalParentPath(node.Metadata);
            try
            {
                var parentOutcome = await ResolveRestoreParentAsync(request, originalParentPath, ct);
                if (parentOutcome.Failure is not null)
                {
                    await tx.RollbackAsync(ct);
                    return parentOutcome.Failure;
                }

                var conflictOutcome = await ResolveConflictAsync(request, parentOutcome.Parent!, node, originalParentPath, ct);
                if (conflictOutcome is not null)
                {
                    await tx.RollbackAsync(ct);
                    return conflictOutcome;
                }

                await RestoreNodeAsync(
                    request,
                    node,
                    wrapperOutcome.Wrapper!,
                    parentOutcome.Parent!,
                    parentOutcome.CreatedParents,
                    ct);
                await tx.CommitAsync(ct);

                return BuildRestoredOutcome(request, node, parentOutcome.Parent!, originalParentPath);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync(ct);
                return BuildConcurrentConflictOutcome(request, node, originalParentPath, ex);
            }
        }

        private async Task<Guid> GetLayoutIdOrThrowAsync(RestoreNodeQuery request, CancellationToken ct)
        {
            Guid layoutId = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .Select(x => x.LayoutId)
                .SingleOrDefaultAsync(ct);

            return layoutId != Guid.Empty
                ? layoutId
                : throw new EntityNotFoundException<Node>();
        }

        private async Task<Node> LoadNodeOrThrowAsync(RestoreNodeQuery request, CancellationToken ct)
        {
            return await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<Node>();
        }

        private async Task<TopLevelTrashWrapperOutcome> ResolveTopLevelTrashWrapperAsync(
            RestoreNodeQuery request,
            Node node,
            CancellationToken ct)
        {
            if (node.Type != NodeType.Trash)
            {
                return TopLevelTrashWrapperOutcome.NotRestorable("Node is not in trash.");
            }

            var wrapper = await LoadWrapperAsync(request.UserId, node.ParentId, ct);
            var trashRoot = await _layouts.GetUserTrashRootAsync(request.UserId, ct);
            if (wrapper is null || wrapper.ParentId != trashRoot.Id)
            {
                return TopLevelTrashWrapperOutcome.NotRestorable("Item can only be restored from the top level of trash.");
            }

            return TopLevelTrashWrapperOutcome.Success(wrapper);
        }

        private async Task<Node?> LoadWrapperAsync(Guid userId, Guid? wrapperId, CancellationToken ct)
        {
            if (!wrapperId.HasValue)
            {
                return null;
            }

            return await _dbContext.Nodes
                .Where(x => x.Id == wrapperId.Value && x.OwnerId == userId)
                .SingleOrDefaultAsync(ct);
        }

        private async Task<RestoreParentOutcome> ResolveRestoreParentAsync(
            RestoreNodeQuery request,
            string originalParentPath,
            CancellationToken ct)
        {
            var resolution = await _restore.ResolveOrCreateParentAsync(
                request.UserId,
                originalParentPath,
                request.CreateMissingParents,
                ct);

            if (resolution.InvalidPathReason is not null)
            {
                return RestoreParentOutcome.Failed(NotRestorable(resolution.InvalidPathReason, originalParentPath));
            }

            return resolution.Parent is null
                ? RestoreParentOutcome.Failed(ParentMissing(originalParentPath))
                : RestoreParentOutcome.Success(resolution.Parent, resolution.CreatedParents);
        }

        private async Task<RestoreOutcomeDto?> ResolveConflictAsync(
            RestoreNodeQuery request,
            Node targetParent,
            Node node,
            string originalParentPath,
            CancellationToken ct)
        {
            var conflict = await _restore.FindConflictAsync(request.UserId, targetParent.Id, node.NameKey, ct);
            if (!conflict.HasValue)
            {
                return null;
            }

            if (!request.Overwrite)
            {
                return Conflict(originalParentPath, conflict.Value.Kind, conflict.Value.Name);
            }

            await _restore.SendConflictToTrashAsync(request.UserId, conflict.Value, ct);
            return null;
        }

        private async Task RestoreNodeAsync(
            RestoreNodeQuery request,
            Node node,
            Node wrapper,
            Node targetParent,
            IReadOnlyList<Node> createdParents,
            CancellationToken ct)
        {
            node.SetParent(targetParent, NodeType.Default);
            node.Metadata = TrashRestoreCoordinator.RemoveOriginalParentPath(node.Metadata);
            await _subtree.SetSubtreeTypeAsync(request.UserId, node.Id, NodeType.Default, ct);
            StageCreatedParents(createdParents);
            _syncChanges.StageFolderChange(SyncChangeKind.FolderRestored, node, targetParent.Id);
            await _dbContext.SaveChangesAsync(ct);
            await _restore.DeleteWrapperIfEmptyAsync(request.UserId, wrapper, ct);
        }

        private void StageCreatedParents(IReadOnlyList<Node> createdParents)
        {
            foreach (Node createdParent in createdParents)
            {
                if (createdParent.ParentId.HasValue)
                {
                    _syncChanges.StageFolderChange(
                        SyncChangeKind.FolderCreated,
                        createdParent,
                        createdParent.ParentId.Value);
                }
            }
        }

        private RestoreOutcomeDto BuildRestoredOutcome(
            RestoreNodeQuery request,
            Node node,
            Node targetParent,
            string originalParentPath)
        {
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

        private RestoreOutcomeDto BuildConcurrentConflictOutcome(
            RestoreNodeQuery request,
            Node node,
            string originalParentPath,
            DbUpdateException ex)
        {
            _logger.LogWarning(
                ex,
                "Concurrent unique violation while restoring node {NodeId} for user {UserId}.",
                request.NodeId,
                request.UserId);

            return Conflict(originalParentPath, RestoreConflictKind.Folder, node.Name);
        }

        private static RestoreOutcomeDto ParentMissing(string originalParentPath) => new()
        {
            Status = RestoreStatus.ParentMissing,
            OriginalParentPath = originalParentPath,
            MissingPath = originalParentPath,
        };

        private static RestoreOutcomeDto Conflict(
            string originalParentPath,
            RestoreConflictKind kind,
            string name) => new()
        {
            Status = RestoreStatus.Conflict,
            OriginalParentPath = originalParentPath,
            ConflictKind = kind,
            ConflictName = name,
        };

        private sealed record TopLevelTrashWrapperOutcome(Node? Wrapper, RestoreOutcomeDto? Failure)
        {
            /// <summary>
            /// Creates a successful operation result.
            /// </summary>
            public static TopLevelTrashWrapperOutcome Success(Node wrapper) => new(wrapper, null);
            /// <summary>
            /// Executes not restorable.
            /// </summary>
            public static TopLevelTrashWrapperOutcome NotRestorable(string reason) => new(null, RestoreNodeQueryHandler.NotRestorable(reason));
        }

        private sealed record RestoreParentOutcome(
            Node? Parent,
            IReadOnlyList<Node> CreatedParents,
            RestoreOutcomeDto? Failure)
        {
            /// <summary>
            /// Creates a successful operation result.
            /// </summary>
            public static RestoreParentOutcome Success(Node parent, IReadOnlyList<Node> createdParents) =>
                new(parent, createdParents, null);
            /// <summary>
            /// Creates a failed operation result.
            /// </summary>
            public static RestoreParentOutcome Failed(RestoreOutcomeDto failure) => new(null, [], failure);
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
