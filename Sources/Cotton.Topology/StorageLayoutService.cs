using Cotton.Database;
using Cotton.Validators;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Topology
{
    public class StorageLayoutService(CottonDbContext _dbContext)
    {
        private static readonly SemaphoreSlim _layoutSemaphore = new(1, 1);

        public async Task<Node> GetUserTrashNodeAsync(Guid ownerId)
        {
            var layout = await GetOrCreateLatestUserLayoutAsync(ownerId);
            return await GetOrCreateRootNodeAsync(layout.Id, ownerId, NodeType.Trash);
        }

        public async Task<Node> GetOrCreateRootNodeAsync(Guid layoutId, Guid ownerId, NodeType nodeType)
        {
            _layoutSemaphore.Wait();
            var currentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Include(x => x.Layout)
                .Where(x => x.Layout.OwnerId == ownerId
                    && x.LayoutId == layoutId
                    && x.ParentId == null
                    && x.Type == nodeType)
                .FirstOrDefaultAsync();
            if (currentNode == null)
            {
                NameValidator.TryNormalizeAndValidate(nodeType.ToString(), out string normalized, out _);
                Node newNode = new()
                {
                    Type = nodeType,
                    OwnerId = ownerId,
                    LayoutId = layoutId,
                };
                newNode.SetName(nodeType.ToString());
                await _dbContext.Nodes.AddAsync(newNode);
                await _dbContext.SaveChangesAsync();
                _layoutSemaphore.Release();
                return newNode;
            }
            _layoutSemaphore.Release();
            return currentNode;
        }

        public async Task<Layout> GetOrCreateLatestUserLayoutAsync(Guid ownerId)
        {
            _layoutSemaphore.Wait();
            var found = await _dbContext.UserLayouts
                .Where(x => x.OwnerId == ownerId && x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
            if (found == null)
            {
                Layout newLayout = new()
                {
                    IsActive = true,
                    OwnerId = ownerId,
                };
                await _dbContext.UserLayouts.AddAsync(newLayout);
                await _dbContext.SaveChangesAsync();
                _layoutSemaphore.Release();
                return newLayout;
            }
            _layoutSemaphore.Release();
            return found;
        }

        public Task<Chunk?> FindChunkAsync(string hash)
        {
            return FindChunkAsync(Convert.FromHexString(hash));
        }

        public async Task<Chunk?> FindChunkAsync(byte[] hash)
        {
            return await _dbContext.Chunks.FindAsync(hash);
        }
    }
}
