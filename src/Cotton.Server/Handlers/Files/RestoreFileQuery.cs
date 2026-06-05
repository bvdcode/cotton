// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Represents a restore file query sent through the mediator pipeline.
    /// </summary>
    public class RestoreFileQuery(Guid userId, Guid nodeFileId, bool createMissingParents, bool overwrite)
        : IRequest<RestoreOutcomeDto>
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
        /// Creates missing parents.
        /// </summary>
        public bool CreateMissingParents { get; } = createMissingParents;
        /// <summary>
        /// Gets whether restore should move an existing conflicting item to trash.
        /// </summary>
        public bool Overwrite { get; } = overwrite;
    }

    /// <summary>
    /// Handles restore file queries in the mediator pipeline.
    /// </summary>
    public class RestoreFileQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        TrashRestoreCoordinator _restore,
        ISyncChangeRecorder _syncChanges,
        ILogger<RestoreFileQueryHandler> _logger)
        : IRequestHandler<RestoreFileQuery, RestoreOutcomeDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<RestoreOutcomeDto> Handle(RestoreFileQuery request, CancellationToken ct)
        {
            Guid layoutId = await GetLayoutIdOrThrowAsync(request, ct);

            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, layoutId, ct);

            var nodeFile = await LoadFileOrThrowAsync(request, ct);
            var wrapper = nodeFile.Node;
            var topLevelFailure = await ValidateTopLevelTrashFileAsync(request.UserId, wrapper, tx, ct);
            if (topLevelFailure is not null)
            {
                return topLevelFailure;
            }

            string originalParentPath = TrashRestoreCoordinator.GetOriginalParentPath(nodeFile.Metadata);
            try
            {
                return await RestoreFileInsideTransactionAsync(request, nodeFile, wrapper, originalParentPath, tx, ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    ex,
                    "Concurrent unique violation while restoring file {NodeFileId} for user {UserId}.",
                    request.NodeFileId,
                    request.UserId);
                return ConflictOutcome(originalParentPath, RestoreConflictKind.File, nodeFile.Name);
            }
        }

        private async Task<Guid> GetLayoutIdOrThrowAsync(RestoreFileQuery request, CancellationToken ct)
        {
            Guid layoutId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .Select(x => x.Node.LayoutId)
                .SingleOrDefaultAsync(ct);
            return layoutId == Guid.Empty ? throw new EntityNotFoundException<NodeFile>() : layoutId;
        }

        private async Task<NodeFile> LoadFileOrThrowAsync(RestoreFileQuery request, CancellationToken ct)
        {
            return await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<NodeFile>();
        }

        private async Task<RestoreOutcomeDto?> ValidateTopLevelTrashFileAsync(
            Guid userId,
            Node wrapper,
            IDbContextTransaction tx,
            CancellationToken ct)
        {
            var trashRoot = await _layouts.GetUserTrashRootAsync(userId);
            if (wrapper.Type == NodeType.Trash && wrapper.ParentId == trashRoot.Id)
            {
                return null;
            }

            await tx.RollbackAsync(ct);
            return NotRestorable("File can only be restored from the top level of trash.");
        }

        private async Task<RestoreOutcomeDto> RestoreFileInsideTransactionAsync(
            RestoreFileQuery request,
            NodeFile nodeFile,
            Node wrapper,
            string originalParentPath,
            IDbContextTransaction tx,
            CancellationToken ct)
        {
            var parentOutcome = await ResolveRestoreParentAsync(request, originalParentPath, tx, ct);
            if (parentOutcome.Failure is not null)
            {
                return parentOutcome.Failure;
            }

            Node targetParent = parentOutcome.Parent!;
            var conflictOutcome = await ResolveConflictAsync(request, targetParent.Id, nodeFile.NameKey, originalParentPath, tx, ct);
            if (conflictOutcome is not null)
            {
                return conflictOutcome;
            }

            nodeFile.NodeId = targetParent.Id;
            nodeFile.Metadata = TrashRestoreCoordinator.RemoveOriginalParentPath(nodeFile.Metadata);
            StageCreatedParents(parentOutcome.CreatedParents);
            _syncChanges.StageFileChange(SyncChangeKind.FileRestored, nodeFile, targetParent.LayoutId);
            await _dbContext.SaveChangesAsync(ct);

            await _restore.DeleteWrapperIfEmptyAsync(request.UserId, wrapper, ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "User {UserId} restored file {NodeFileId} into parent {ParentId}.",
                request.UserId,
                nodeFile.Id,
                targetParent.Id);

            return new RestoreOutcomeDto
            {
                Status = RestoreStatus.Restored,
                OriginalParentPath = originalParentPath,
                RestoredFile = nodeFile.Adapt<NodeFileManifestDto>(),
            };
        }

        private async Task<RestoreParentOutcome> ResolveRestoreParentAsync(
            RestoreFileQuery request,
            string originalParentPath,
            IDbContextTransaction tx,
            CancellationToken ct)
        {
            var resolution = await _restore.ResolveOrCreateParentAsync(
                request.UserId,
                originalParentPath,
                request.CreateMissingParents,
                ct);
            if (resolution.InvalidPathReason is not null)
            {
                await tx.RollbackAsync(ct);
                return new RestoreParentOutcome(
                    null,
                    [],
                    NotRestorable(resolution.InvalidPathReason, originalParentPath));
            }

            if (resolution.Parent is null)
            {
                await tx.RollbackAsync(ct);
                return new RestoreParentOutcome(null, [], ParentMissingOutcome(originalParentPath));
            }

            return new RestoreParentOutcome(resolution.Parent, resolution.CreatedParents, null);
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

        private async Task<RestoreOutcomeDto?> ResolveConflictAsync(
            RestoreFileQuery request,
            Guid targetParentId,
            string nameKey,
            string originalParentPath,
            IDbContextTransaction tx,
            CancellationToken ct)
        {
            var conflict = await _restore.FindConflictAsync(request.UserId, targetParentId, nameKey, ct);
            if (!conflict.HasValue)
            {
                return null;
            }

            if (!request.Overwrite)
            {
                await tx.RollbackAsync(ct);
                return ConflictOutcome(originalParentPath, conflict.Value.Kind, conflict.Value.Name);
            }

            await _restore.SendConflictToTrashAsync(request.UserId, conflict.Value, ct);
            return null;
        }

        private static RestoreOutcomeDto ParentMissingOutcome(string originalParentPath) => new()
        {
            Status = RestoreStatus.ParentMissing,
            OriginalParentPath = originalParentPath,
            MissingPath = originalParentPath,
        };

        private static RestoreOutcomeDto ConflictOutcome(
            string originalParentPath,
            RestoreConflictKind conflictKind,
            string conflictName) => new()
        {
            Status = RestoreStatus.Conflict,
            OriginalParentPath = originalParentPath,
            ConflictKind = conflictKind,
            ConflictName = conflictName,
        };

        private sealed record RestoreParentOutcome(
            Node? Parent,
            IReadOnlyList<Node> CreatedParents,
            RestoreOutcomeDto? Failure);

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
