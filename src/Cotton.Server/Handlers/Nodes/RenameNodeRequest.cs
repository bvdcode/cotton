// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Nodes;
using Cotton.Server.Abstractions;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Renames an owned node.
    /// </summary>
    public record RenameNodeRequest(
        Guid UserId,
        Guid NodeId,
        string Name) : IRequest<RenameNodeResult>;

    /// <summary>
    /// Handles node rename requests.
    /// </summary>
    public class RenameNodeRequestHandler(
        CottonDbContext _dbContext,
        ISyncChangeRecorder _syncChanges,
        ILayoutMutationGate _layoutGate)
        : IRequestHandler<RenameNodeRequest, RenameNodeResult>
    {
        /// <inheritdoc />
        public async Task<RenameNodeResult> Handle(
            RenameNodeRequest request,
            CancellationToken ct)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(
                request.Name,
                out _,
                out string? errorMessage);
            if (!isValidName)
            {
                return Failure(RenameNodeStatus.InvalidName, errorMessage);
            }

            Guid? layoutId = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == request.UserId)
                .Select(x => (Guid?)x.LayoutId)
                .SingleOrDefaultAsync(ct);
            if (layoutId is null)
            {
                return Failure(RenameNodeStatus.NodeNotFound, "Node not found.");
            }

            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(
                layoutId.Value,
                ct);
            await using IDbContextTransaction tx = await _dbContext.Database
                .BeginTransactionAsync(ct);

            Node? node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct);
            if (node is null)
            {
                return Failure(RenameNodeStatus.NodeNotFound, "Node not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);
            string? conflict = await FindNameConflictAsync(
                node,
                request,
                nameKey,
                ct);
            if (conflict is not null)
            {
                return Failure(RenameNodeStatus.NameConflict, conflict);
            }

            node.SetName(request.Name);
            if (node.ParentId.HasValue)
            {
                _syncChanges.StageFolderChange(
                    SyncChangeKind.FolderRenamed,
                    node,
                    node.ParentId.Value);
            }

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new RenameNodeResult(
                RenameNodeStatus.Renamed,
                node.Adapt<NodeDto>());
        }

        private async Task<string?> FindNameConflictAsync(
            Node node,
            RenameNodeRequest request,
            string nameKey,
            CancellationToken ct)
        {
            bool nodeExists = await _dbContext.Nodes.AnyAsync(
                x => x.ParentId == node.ParentId
                    && x.OwnerId == request.UserId
                    && x.NameKey == nameKey
                    && x.LayoutId == node.LayoutId
                    && x.Type == node.Type
                    && x.Id != request.NodeId,
                ct);
            if (nodeExists)
            {
                return "A folder with the same name key already exists in the parent folder: " + nameKey;
            }

            if (!node.ParentId.HasValue)
            {
                return null;
            }

            bool fileExists = await _dbContext.NodeFiles.AnyAsync(
                x => x.NodeId == node.ParentId.Value
                    && x.OwnerId == request.UserId
                    && x.NameKey == nameKey,
                ct);
            return fileExists
                ? "A file with the same name key already exists in the parent folder: " + nameKey
                : null;
        }

        private static RenameNodeResult Failure(
            RenameNodeStatus status,
            string? error) => new(status, Error: error);
    }
}
