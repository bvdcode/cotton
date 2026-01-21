using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Topology;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public class DeleteFileQuery(Guid userId, Guid nodeFileId, bool skipTrash) : ICommand
    {
        public Guid UserId { get; } = userId;
        public Guid NodeFileId { get; } = nodeFileId;
        public bool SkipTrash { get; } = skipTrash;
    }

    public class DeleteFileQueryHandler(
        CottonDbContext _dbContext,
        StorageLayoutService _layouts,
        ILogger<DeleteFileQueryHandler> _logger)
            : ICommandHandler<DeleteFileQuery>
    {
        public async ValueTask<Unit> Handle(DeleteFileQuery command, CancellationToken ct)
        {
            if (command.SkipTrash)
            {
                NodeFile nodeFile = await _dbContext.NodeFiles
                    .FirstOrDefaultAsync(x => x.Id == command.NodeFileId
                        && x.OwnerId == command.UserId, cancellationToken: ct)
                        ?? throw new EntityNotFoundException(nameof(FileManifest));

                _dbContext.NodeFiles.Remove(nodeFile);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("User {UserId} permanently deleted file {NodeFileId}.",
                    command.UserId, command.NodeFileId);
            }
            else
            {
                var nodeFile = await _dbContext.NodeFiles
                    .Include(x => x.Node)
                    .FirstOrDefaultAsync(x => x.Id == command.NodeFileId
                        && x.OwnerId == command.UserId, cancellationToken: ct)
                        ?? throw new EntityNotFoundException(nameof(FileManifest));
                if (nodeFile.Node.Type == NodeType.Trash)
                {
                    throw new EntityNotFoundException(nameof(FileManifest));
                }
                var trashItem = await _layouts.CreateTrashItemAsync(command.UserId);
                nodeFile.NodeId = trashItem.Id;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("User {UserId} deleted file {NodeFileId} to trash.",
                    command.UserId, command.NodeFileId);
            }
            return default;
        }
    }
}
