// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Nodes;
using Cotton.Server.Services;
using Cotton.Topology;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Nodes
{
    public record CopyNodeCommand(Guid UserId, Guid NodeId, Guid ParentId, string? Name) : IRequest<NodeDto>;

    public class CopyNodeCommandHandler(
        CottonDbContext _dbContext,
        IMediator _mediator,
        ILayoutMutationGate _layoutGate,
        UserStorageQuotaService _quota,
        ISyncChangeRecorder _syncChanges,
        IEventNotificationService _events,
        ILogger<CopyNodeCommandHandler> _logger) : IRequestHandler<CopyNodeCommand, NodeDto>
    {
        private const int BatchSize = 256;

        public async Task<NodeDto> Handle(CopyNodeCommand request, CancellationToken ct)
        {
            Node source = await LoadSourceAsync(request, ct);
            await using IAsyncDisposable gate = await _layoutGate.EnterAsync(source.LayoutId, ct);
            Node copy;
            await using (IAsyncDisposable quotaGate = await _quota.EnterMutationAsync(request.UserId, ct))
            await using (IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct))
            {
                source = await LoadSourceAsync(request, ct);
                await ValidateTargetAsync(source, request.ParentId, ct);
                Node parent = await _mediator.Send(new PrepareCopyDestinationRequest(
                    request.UserId, source.LayoutId, request.ParentId, request.Name ?? source.Name), ct);
                copy = source.CopyTo(parent, request.Name ?? source.Name);
                _dbContext.Nodes.Add(copy);
                _syncChanges.StageFolderChange(SyncChangeKind.FolderCreated, copy, parent.Id);
                await SaveBatchAsync(ct);
                long addedBytes = await CopyContentsAsync(source, copy, 0, ct);
                await transaction.CommitAsync(ct);
                _quota.RecordLogicalBytesAdded(request.UserId, addedBytes);
            }
            try
            {
                await _events.NotifyNodeCreatedAsync(copy.Id, ct);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Copy notification failed for folder {NodeId}", copy.Id);
            }
            return copy.Adapt<NodeDto>();
        }

        private async Task<Node> LoadSourceAsync(CopyNodeCommand request, CancellationToken ct)
        {
            return await _dbContext.Nodes.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == request.NodeId && x.OwnerId == request.UserId
                    && x.Type == NodeType.Default && x.ParentId != null, ct)
                ?? throw new EntityNotFoundException<Node>("Source folder not found.");
        }

        private async Task ValidateTargetAsync(Node source, Guid parentId, CancellationToken ct)
        {
            try
            {
                await foreach (Node ancestor in NodeHierarchy.ReadAncestorsAsync(
                    _dbContext.Nodes.AsNoTracking().Where(x => x.OwnerId == source.OwnerId && x.LayoutId == source.LayoutId),
                    parentId, maxDepth: int.MaxValue, cancellationToken: ct))
                {
                    if (ancestor.Id == source.Id)
                    {
                        throw new BadRequestException<Node>("Cannot copy a folder into itself or its descendant.");
                    }
                }
            }
            catch (NodeHierarchyException exception)
            {
                _logger.LogWarning(exception, "Cannot copy within an invalid folder hierarchy");
                throw new BadRequestException<Node>("Folder hierarchy contains a cycle.");
            }
        }

        private async Task<long> CopyContentsAsync(Node source, Node target, long addedBytes, CancellationToken ct)
        {
            Guid? afterFileId = null;
            while (true)
            {
                List<NodeFile> files = await _dbContext.NodeFiles.AsNoTracking().Include(x => x.FileManifest)
                    .Where(x => x.NodeId == source.Id && x.OwnerId == source.OwnerId
                        && (!afterFileId.HasValue || x.Id.CompareTo(afterFileId.Value) > 0))
                    .OrderBy(x => x.Id).Take(BatchSize).ToListAsync(ct);
                if (files.Count == 0)
                {
                    break;
                }
                long batchBytes = files.Sum(x => x.FileManifest.SizeBytes);
                await _quota.ReserveFileReferencesAsync(source.OwnerId, batchBytes, ct);
                addedBytes = checked(addedBytes + batchBytes);
                foreach (NodeFile file in files)
                {
                    NodeFile copy = file.CopyTo(target, file.Name);
                    _dbContext.NodeFiles.Add(copy);
                    _syncChanges.StageFileChange(SyncChangeKind.FileCreated, copy, target.LayoutId);
                }
                await SaveBatchAsync(ct);
                afterFileId = files[^1].Id;
            }

            Guid? afterNodeId = null;
            while (true)
            {
                List<Node> children = await _dbContext.Nodes.AsNoTracking()
                    .Where(x => x.ParentId == source.Id && x.OwnerId == source.OwnerId
                        && x.LayoutId == source.LayoutId && x.Type == NodeType.Default
                        && (!afterNodeId.HasValue || x.Id.CompareTo(afterNodeId.Value) > 0))
                    .OrderBy(x => x.Id).Take(BatchSize).ToListAsync(ct);
                if (children.Count == 0)
                {
                    break;
                }
                foreach (Node child in children)
                {
                    Node copy = child.CopyTo(target, child.Name);
                    _dbContext.Nodes.Add(copy);
                    _syncChanges.StageFolderChange(SyncChangeKind.FolderCreated, copy, target.Id);
                    await SaveBatchAsync(ct);
                    addedBytes = await CopyContentsAsync(child, copy, addedBytes, ct);
                }
                afterNodeId = children[^1].Id;
            }
            return addedBytes;
        }

        private async Task SaveBatchAsync(CancellationToken ct)
        {
            await _dbContext.SaveChangesAsync(ct);
            _dbContext.ChangeTracker.Clear();
        }
    }
}
