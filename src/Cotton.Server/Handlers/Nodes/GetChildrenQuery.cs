using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public class GetChildrenQuery(
        Guid userId, Guid nodeId, NodeType nodeType,
        int page, int pageSize, int depth = 0) : IRequest<NodeContentDto>
    {
        public Guid UserId { get; } = userId;
        public Guid NodeId { get; } = nodeId;
        public NodeType NodeType { get; } = nodeType;
        public int Page { get; } = page;
        public int PageSize { get; } = pageSize;
        public int Depth { get; } = depth;
    }

    public class GetChildrenQueryHandler(
        ILayoutService _layouts,
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
            ArgumentOutOfRangeException.ThrowIfNegative(request.Depth);

            // Resolve the set of parent IDs whose children should be returned.
            // depth == 0: direct children of parentNode (default).
            // depth == N: skip N intermediate levels and return their descendants.
            IQueryable<Guid> parentIds = _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == parentNode.Id)
                .Select(x => x.Id);

            for (int i = 0; i < request.Depth; i++)
            {
                parentIds = _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.ParentId != null
                        && parentIds.Contains(x.ParentId.Value)
                        && x.OwnerId == request.UserId
                        && x.LayoutId == layout.Id
                        && x.Type == request.NodeType)
                    .Select(x => x.Id);
            }

            int skip = (request.Page - 1) * request.PageSize;
            IQueryable<NodeDto> nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .OrderBy(x => x.NameKey)
                .Where(x => x.ParentId != null
                    && parentIds.Contains(x.ParentId.Value)
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == request.NodeType)
                .ProjectToType<NodeDto>();

            var filesBaseQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => parentIds.Contains(x.NodeId));

            int nodesCount = await nodesQuery.CountAsync(cancellationToken: ct);
            int filesCount = await filesBaseQuery.CountAsync(cancellationToken: ct);

            var nodesToTake = Math.Max(0, Math.Min(request.PageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, request.PageSize - nodesToTake);

            var nodes = nodesToTake == 0 ? []
                : await nodesQuery.Skip(skip).Take(nodesToTake).ToListAsync(cancellationToken: ct);

            var files = filesToTake == 0 ? []
                : await filesBaseQuery
                    .OrderBy(x => x.NameKey)
                    .Include(x => x.FileManifest)
                    .Skip(filesSkip)
                    .Take(filesToTake)
                    .ProjectToType<NodeFileManifestDto>()
                    .ToListAsync(cancellationToken: ct);

            return new()
            {
                Nodes = nodes,
                Files = files,
                Id = request.NodeId,
                CreatedAt = parentNode.CreatedAt,
                UpdatedAt = parentNode.UpdatedAt,
                TotalCount = nodesCount + filesCount,
            };
        }
    }
}
