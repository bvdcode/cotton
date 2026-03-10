using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    public class GetRecentNodesQuery(Guid userId, Guid layoutId, int count) : IRequest<IEnumerable<NodeFileManifestDto>>
    {
        public int Count { get; } = count;
        public Guid UserId { get; } = userId;
        public Guid LayoutId { get; } = layoutId;
    }

    public class GetRecentNodesQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetRecentNodesQuery, IEnumerable<NodeFileManifestDto>>
    {
        public async Task<IEnumerable<NodeFileManifestDto>> Handle(GetRecentNodesQuery request, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Count);
            return await _dbContext.NodeFiles
                .AsNoTracking()
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.OwnerId == request.UserId && x.Node.LayoutId == request.LayoutId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(request.Count)
                .ProjectToType<NodeFileManifestDto>()
                .ToListAsync(ct);
        }
    }
}
