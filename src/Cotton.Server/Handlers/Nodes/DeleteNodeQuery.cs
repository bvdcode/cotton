using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Nodes
{
    public class DeleteNodeQuery(Guid userId, Guid nodeId, bool skipTrash) : IRequest
    {
        public Guid UserId { get; } = userId;
        public Guid NodeId { get; } = nodeId;
        public bool SkipTrash { get; } = skipTrash;
    }

    public class DeleteNodeQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ILayoutNavigator _navigator,
        NodeSubtreeService _subtree,
        ILogger<DeleteNodeQueryHandler> _logger,
        UserStorageQuotaService _quota,
        FileVersionService _versions)
            : IRequestHandler<DeleteNodeQuery>
    {
        public async Task Handle(DeleteNodeQuery request, CancellationToken ct)
        {
            var node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(Node));
            if (node.ParentId == null)
            {
                throw new InvalidOperationException("Cannot delete root node.");
            }
            if (request.SkipTrash)
            {
                await DeletePermanentlyAsync(request, node, ct);
            }
            else
            {
                await MoveToTrashAsync(request, node, ct);
            }
        }

        private async Task MoveToTrashAsync(DeleteNodeQuery command, Node node, CancellationToken ct)
        {
            await using IDbContextTransaction? tx = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            await LayoutLocks.AcquireForLayoutAsync(_dbContext, node.LayoutId, ct);

            node = await _dbContext.Nodes
                    .Where(x => x.Id == command.NodeId && x.OwnerId == command.UserId)
                    .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException(nameof(Node));

            if (node.ParentId is null || node.Type != NodeType.Default)
            {
                throw new EntityNotFoundException(nameof(Node));
            }

            string? originalParentPath = await _navigator.GetNodePathFromRootAsync(
                command.UserId,
                node.ParentId.Value,
                node.Type,
                ct);
            if (originalParentPath is not null)
            {
                node.Metadata = TrashRestoreCoordinator.SetOriginalParentPath(
                    node.Metadata,
                    originalParentPath);
            }

            Node trashItem = await _layouts.CreateTrashItemAsync(command.UserId);
            node.SetParent(trashItem, NodeType.Trash);

            // Ensure the whole subtree is considered trash for browsing/search/filtering.
            await MoveDescendantsToTrashAsync(command.UserId, node.Id, ct);

            await _dbContext.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            _logger.LogInformation("User {UserId} deleted node {NodeId} to trash.",
                command.UserId, command.NodeId);
        }

        private async Task MoveDescendantsToTrashAsync(Guid userId, Guid rootId, CancellationToken ct)
        {
            var ids = (await _subtree.CollectSubtreeIdsAsync(userId, rootId, ct)).ToArray();

            await _dbContext.Nodes
                .Where(x => x.OwnerId == userId && ids.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Type, NodeType.Trash), ct);

            await _dbContext.DownloadTokens
                .Where(t => t.CreatedByUserId == userId && ids.Contains(t.NodeFile.NodeId)
                    && (!t.ExpiresAt.HasValue || t.ExpiresAt.Value > DateTime.UtcNow))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.ExpiresAt, DateTime.UtcNow), ct);
        }

        private async Task DeletePermanentlyAsync(DeleteNodeQuery command, Node node, CancellationToken ct)
        {
            var nodeIds = await _subtree.CollectSubtreeIdsAsync(command.UserId, node.Id, ct);

            if (await _versions.ContainsHistoricalVersionsAsync(command.UserId, nodeIds, ct))
            {
                throw new BadRequestException<Node>("File version containers cannot be deleted directly.");
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

            await _dbContext.DownloadTokens
                .Where(t => t.CreatedByUserId == command.UserId && nodeIds.Contains(t.NodeFile.NodeId))
                .ExecuteDeleteAsync(ct);

            long removedBytes = await _dbContext.NodeFiles
                .Where(x => x.OwnerId == command.UserId && nodeIds.Contains(x.NodeId))
                .SumAsync(x => (long?)x.FileManifest.SizeBytes, ct) ?? 0;

            var nodeFiles = await _dbContext.NodeFiles
                .Where(x => x.OwnerId == command.UserId && nodeIds.Contains(x.NodeId))
                .ToListAsync(ct);
            _dbContext.NodeFiles.RemoveRange(nodeFiles);

            var nodesToDelete = await _dbContext.Nodes
                .Where(x => x.OwnerId == command.UserId && nodeIds.Contains(x.Id))
                .ToListAsync(ct);

            // Delete deepest nodes first to satisfy self-referencing FK restrictions.
            // A simple leaves-first order: nodes with non-null ParentId first.
            nodesToDelete.Sort((a, b) => (a.ParentId is null).CompareTo(b.ParentId is null));

            _dbContext.Nodes.RemoveRange(nodesToDelete);
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            _quota.RecordLogicalBytesRemoved(command.UserId, removedBytes);

            _logger.LogInformation("User {UserId} permanently deleted node {NodeId} recursively ({Count} nodes, {Files} files).",
                command.UserId, command.NodeId, nodesToDelete.Count, nodeFiles.Count);
        }
    }
}
