using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public class DeleteFileQuery(Guid userId, Guid nodeFileId, bool skipTrash) : IRequest
    {
        public Guid UserId { get; } = userId;
        public Guid NodeFileId { get; } = nodeFileId;
        public bool SkipTrash { get; } = skipTrash;
    }

    public class DeleteFileQueryHandler(
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ILogger<DeleteFileQueryHandler> _logger)
            : IRequestHandler<DeleteFileQuery>
    {
        public async Task Handle(DeleteFileQuery request, CancellationToken ct)
        {
            NodeFile nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .FirstOrDefaultAsync(x => x.Id == request.NodeFileId
                    && x.OwnerId == request.UserId, cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(FileManifest));
            if (request.SkipTrash)
            {
                await DeletePermanentlyAsync(request, nodeFile, ct);
            }
            else
            {
                await MoveToTrashAsync(request, nodeFile, ct);
            }
        }

        private async Task DeletePermanentlyAsync(DeleteFileQuery command, NodeFile nodeFile, CancellationToken ct)
        {
            _dbContext.NodeFiles.Remove(nodeFile);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("User {UserId} permanently deleted file {NodeFileId}.",
                command.UserId, command.NodeFileId);
        }

        private async Task MoveToTrashAsync(DeleteFileQuery command, NodeFile nodeFile, CancellationToken ct)
        {
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
    }
}
