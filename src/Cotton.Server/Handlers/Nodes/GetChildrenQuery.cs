using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Topology;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public class GetChildrenQuery(
        Guid userId, Guid nodeId, NodeType nodeType,
        int page, int pageSize) : IRequest<NodeContentDto>
    {
        public Guid UserId { get; } = userId;
        public Guid NodeId { get; } = nodeId;
        public NodeType NodeType { get; } = nodeType;
        public int Page { get; } = page;
        public int PageSize { get; } = pageSize;
    }

    public class GetChildrenQueryHandler(
        StorageLayoutService _layouts,
        CottonDbContext _dbContext)
            : IRequestHandler<GetChildrenQuery, NodeContentDto>
    {
        public async Task<NodeContentDto> Handle(GetChildrenQuery request, CancellationToken ct)
        {
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);
            var parentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == request.NodeType)
                .SingleOrDefaultAsync(cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(Node));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Page);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.PageSize);

            int skip = (request.Page - 1) * request.PageSize;
            IQueryable<NodeDto> nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .OrderBy(x => x.NameKey)
                .Where(x => x.ParentId == parentNode.Id
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == request.NodeType)
                .ProjectToType<NodeDto>();

            var filesQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .OrderBy(x => x.NameKey)
                .Where(x => x.NodeId == parentNode.Id)
                .ProjectToType<FileManifestDto>();

            int nodesCount = await nodesQuery.CountAsync(cancellationToken: ct);
            int filesCount = await filesQuery.CountAsync(cancellationToken: ct);

            var nodesToTake = Math.Max(0, Math.Min(request.PageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, request.PageSize - nodesToTake);

            var nodes = nodesToTake == 0 ? []
                : await nodesQuery.Skip(skip).Take(nodesToTake).ToListAsync(cancellationToken: ct);

            var files = filesToTake == 0 ? []
                : await filesQuery.Skip(filesSkip).Take(filesToTake).ToListAsync(cancellationToken: ct);

            return new()
            {
                Nodes = nodes,
                Files = files,
                Id = request.NodeId,
                CreatedAt = parentNode.CreatedAt,
                UpdatedAt = parentNode.UpdatedAt
            };
        }
    }
}
