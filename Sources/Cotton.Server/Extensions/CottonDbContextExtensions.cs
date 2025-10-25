using Cotton.Server.Helpers;
using Cotton.Server.Database;
using Cotton.Server.Database.Models;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models.Enums;

namespace Cotton.Server.Extensions
{
    public static class CottonDbContextExtensions
    {
        public static async Task<Node> GetRootNodeAsync(this CottonDbContext dbContext, Guid layoutId, Guid ownerId, UserLayoutNodeType type)
        {
            var currentNode = await dbContext.UserLayoutNodes
                .AsNoTracking()
                .Include(x => x.Layout)
                .Where(x => x.Layout.OwnerId == ownerId
                    && x.LayoutId == layoutId
                    && x.ParentId == null
                    && x.Type == type)
                .FirstOrDefaultAsync();
            if (currentNode == null)
            {
                Node newNode = new()
                {
                    Name = "/",
                    Type = type,
                    OwnerId = ownerId,
                    LayoutId = layoutId,
                };
                await dbContext.UserLayoutNodes.AddAsync(newNode);
                await dbContext.SaveChangesAsync();
                return newNode;
            }
            return currentNode;
        }

        public static async Task<Layout> GetLatestUserLayoutAsync(this CottonDbContext dbContext, Guid ownerId)
        {
            var found = await dbContext.UserLayouts
                .Where(x => x.OwnerId == ownerId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
            if (found == null)
            {
                Layout newLayout = new()
                {
                    OwnerId = ownerId
                };
                await dbContext.UserLayouts.AddAsync(newLayout);
                await dbContext.SaveChangesAsync();
                return newLayout;
            }
            return found;
        }

        public static Task<Chunk?> FindChunkAsync(this CottonDbContext dbContext, string sha256hex)
        {
            if (!HashHelpers.IsValidHash(sha256hex))
            {
                throw new ArgumentException("Invalid hash format.", nameof(sha256hex));
            }
            return FindChunkAsync(dbContext, Convert.FromHexString(sha256hex));
        }

        public static async Task<Chunk?> FindChunkAsync(this CottonDbContext dbContext, byte[] sha256)
        {
            return await dbContext.Chunks.FindAsync(sha256);
        }
    }
}
