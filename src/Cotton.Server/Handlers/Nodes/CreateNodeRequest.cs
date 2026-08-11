// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
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
    /// Creates a folder node in an owned layout.
    /// </summary>
    public record CreateNodeRequest(
        Guid UserId,
        Guid ParentId,
        string Name) : IRequest<CreateNodeResult>;

    /// <summary>
    /// Handles node creation requests.
    /// </summary>
    public class CreateNodeRequestHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ISyncChangeRecorder _syncChanges,
        ILayoutMutationGate _layoutGate)
        : IRequestHandler<CreateNodeRequest, CreateNodeResult>
    {
        /// <inheritdoc />
        public async Task<CreateNodeResult> Handle(
            CreateNodeRequest request,
            CancellationToken ct)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(
                request.Name,
                out _,
                out string? errorMessage);
            if (!isValidName)
            {
                return Failure(CreateNodeStatus.InvalidName, errorMessage);
            }

            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(
                request.UserId,
                ct);
            bool parentExists = await _dbContext.Nodes
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ParentId
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == NodeType.Default,
                    ct);
            if (!parentExists)
            {
                return Failure(
                    CreateNodeStatus.ParentNotFound,
                    "Parent node not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);
            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(
                layout.Id,
                ct);
            await using IDbContextTransaction tx = await _dbContext.Database
                .BeginTransactionAsync(ct);

            Node? parentNode = await _dbContext.Nodes
                .Where(x => x.Id == request.ParentId
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);
            if (parentNode is null)
            {
                return Failure(
                    CreateNodeStatus.ParentNotFound,
                    "Parent node not found.");
            }

            string? conflict = await FindNameConflictAsync(
                parentNode,
                request,
                nameKey,
                ct);
            if (conflict is not null)
            {
                return Failure(CreateNodeStatus.NameConflict, conflict);
            }

            Node node = new()
            {
                OwnerId = request.UserId,
                Type = NodeType.Default,
                LayoutId = layout.Id,
            };
            node.SetParent(parentNode);
            node.SetName(request.Name);
            await _dbContext.Nodes.AddAsync(node, ct);
            _syncChanges.StageFolderChange(
                SyncChangeKind.FolderCreated,
                node,
                parentNode.Id);
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new CreateNodeResult(
                CreateNodeStatus.Created,
                node.Adapt<NodeDto>());
        }

        private async Task<string?> FindNameConflictAsync(
            Node parentNode,
            CreateNodeRequest request,
            string nameKey,
            CancellationToken ct)
        {
            bool nodeExists = await _dbContext.Nodes.AnyAsync(
                x => x.ParentId == parentNode.Id
                    && x.OwnerId == request.UserId
                    && x.NameKey == nameKey
                    && x.LayoutId == parentNode.LayoutId
                    && x.Type == NodeType.Default,
                ct);
            if (nodeExists)
            {
                return "A folder with the same name key already exists in the target layout: " + nameKey;
            }

            bool fileExists = await _dbContext.NodeFiles.AnyAsync(
                x => x.NodeId == parentNode.Id
                    && x.OwnerId == request.UserId
                    && x.NameKey == nameKey,
                ct);
            return fileExists
                ? "A file with the same name key already exists in the target layout: " + nameKey
                : null;
        }

        private static CreateNodeResult Failure(
            CreateNodeStatus status,
            string? error) => new(status, Error: error);
    }
}
