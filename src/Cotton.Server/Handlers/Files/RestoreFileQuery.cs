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

namespace Cotton.Server.Handlers.Files
{
    public class RestoreFileQuery(Guid userId, Guid nodeFileId, bool createMissingParents, bool overwrite)
        : IRequest<RestoreOutcomeDto>
    {
        public Guid UserId { get; } = userId;
        public Guid NodeFileId { get; } = nodeFileId;
        public bool CreateMissingParents { get; } = createMissingParents;
        public bool Overwrite { get; } = overwrite;
    }

    public class RestoreFileQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        TrashRestoreCoordinator _restore,
        ILogger<RestoreFileQueryHandler> _logger)
        : IRequestHandler<RestoreFileQuery, RestoreOutcomeDto>
    {
        public async Task<RestoreOutcomeDto> Handle(RestoreFileQuery request, CancellationToken ct)
        {
            Guid layoutId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .Select(x => x.Node.LayoutId)
                .SingleOrDefaultAsync(ct);
            if (layoutId == Guid.Empty)
            {
                throw new EntityNotFoundException<NodeFile>();
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, layoutId, ct);

            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<NodeFile>();

            var wrapper = nodeFile.Node;
            var trashRoot = await _layouts.GetUserTrashRootAsync(request.UserId);
            if (wrapper.Type != NodeType.Trash || wrapper.ParentId != trashRoot.Id)
            {
                await tx.RollbackAsync(ct);
                return NotRestorable("File can only be restored from the top level of trash.");
            }

            string originalParentPath = TrashRestoreCoordinator.GetOriginalParentPath(nodeFile.Metadata);

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

                var conflict = await _restore.FindConflictAsync(request.UserId, targetParent.Id, nodeFile.NameKey, ct);
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

                nodeFile.NodeId = targetParent.Id;
                nodeFile.Metadata = TrashRestoreCoordinator.RemoveOriginalParentPath(nodeFile.Metadata);
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
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    ex,
                    "Concurrent unique violation while restoring file {NodeFileId} for user {UserId}.",
                    request.NodeFileId,
                    request.UserId);
                return new RestoreOutcomeDto
                {
                    Status = RestoreStatus.Conflict,
                    OriginalParentPath = originalParentPath,
                    ConflictKind = RestoreConflictKind.File,
                    ConflictName = nodeFile.Name,
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
