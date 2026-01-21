
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models;
using Cotton.Topology;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public class DeleteNodeQuery(Guid userId, Guid nodeId, bool skipTrash) : ICommand
    {
        public Guid UserId { get; } = userId;
        public Guid NodeId { get; } = nodeId;
        public bool SkipTrash { get; } = skipTrash;
    }

    public class DeleteNodeQueryHandler(
        CottonDbContext _dbContext,
        StorageLayoutService _layouts,
        ILogger<DeleteNodeQueryHandler> _logger)
            : ICommandHandler<DeleteNodeQuery>
    {
        public async ValueTask<Unit> Handle(DeleteNodeQuery command, CancellationToken ct)
        {
            var node = await _dbContext.Nodes
                .Where(x => x.Id == command.NodeId && x.OwnerId == command.UserId)
                .SingleOrDefaultAsync(cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(Node));
            if (command.SkipTrash)
            {
                // recursively delete all child nodes and node files

            }
            else
            {
                Node trashItem = await _layouts.CreateTrashItemAsync(command.UserId);
                node.ParentId = trashItem.Id;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("User {UserId} deleted node {NodeId} to trash.",
                    command.UserId, command.NodeId);
            }
            return default;
        }
    }
}
