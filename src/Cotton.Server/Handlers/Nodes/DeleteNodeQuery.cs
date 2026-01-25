using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

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
        ILogger<DeleteNodeQueryHandler> _logger)
            : IRequestHandler<DeleteNodeQuery>
    {
        public async Task Handle(DeleteNodeQuery request, CancellationToken ct)
        {
            var node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(Node));
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
            Node trashItem = await _layouts.CreateTrashItemAsync(command.UserId);
            node.ParentId = trashItem.Id;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("User {UserId} deleted node {NodeId} to trash.",
                command.UserId, command.NodeId);
        }

        private async Task DeletePermanentlyAsync(DeleteNodeQuery command, Node node, CancellationToken ct)
        {
            var nodeIds = new HashSet<Guid>();
            var frontier = new List<Guid> { node.Id };
            while (frontier.Count > 0)
            {
                var batch = frontier.ToArray();
                frontier.Clear();

                foreach (var id in batch)
                {
                    nodeIds.Add(id);
                }

                var childIds = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.OwnerId == command.UserId
                        && x.ParentId != null
                        && batch.Contains(x.ParentId.Value))
                    .Select(x => x.Id)
                    .ToListAsync(ct);

                foreach (var childId in childIds)
                {
                    if (nodeIds.Add(childId))
                    {
                        frontier.Add(childId);
                    }
                }
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

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

            _logger.LogInformation("User {UserId} permanently deleted node {NodeId} recursively ({Count} nodes, {Files} files).",
                command.UserId, command.NodeId, nodesToDelete.Count, nodeFiles.Count);
        }
    }
}
