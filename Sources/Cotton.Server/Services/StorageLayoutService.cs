using Cotton.Server.Helpers;
using Cotton.Server.Database;
using Cotton.Server.Validators;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using Cotton.Server.Database.Models.Enums;

namespace Cotton.Server.Services
{
    public class StorageLayoutService(CottonDbContext _dbContext)
    {
        private static readonly SemaphoreSlim _layoutSemaphore = new(1, 1);

        public async Task<Node> GetUserTrashNodeAsync(Guid ownerId)
        {
            var layout = await GetOrCreateLatestUserLayoutAsync(ownerId);
            return await GetOrCreateRootNodeAsync(layout.Id, ownerId, NodeType.Trash);
        }

        public async Task<Node> GetOrCreateRootNodeAsync(Guid layoutId, Guid ownerId, NodeType type)
        {
            _layoutSemaphore.Wait();
            var currentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Include(x => x.Layout)
                .Where(x => x.Layout.OwnerId == ownerId
                    && x.LayoutId == layoutId
                    && x.ParentId == null
                    && x.Type == type)
                .FirstOrDefaultAsync();
            if (currentNode == null)
            {
                NameValidator.TryNormalizeAndValidate(type.ToString(), out string normalized, out _);
                Node newNode = new()
                {
                    Type = type,
                    OwnerId = ownerId,
                    LayoutId = layoutId,
                };
                newNode.SetName(type.ToString());
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

        public Task<Chunk?> FindChunkAsync(string sha256hex)
        {
            if (!HashHelpers.IsValidHash(sha256hex))
            {
                throw new ArgumentException("Invalid hash format.", nameof(sha256hex));
            }
            return FindChunkAsync(Convert.FromHexString(sha256hex));
        }

        public async Task<Chunk?> FindChunkAsync(byte[] sha256)
        {
            return await _dbContext.Chunks.FindAsync(sha256);
        }
    }
}
