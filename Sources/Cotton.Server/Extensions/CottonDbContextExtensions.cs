using Cotton.Server.Helpers;
using Cotton.Server.Database;
using Cotton.Server.Database.Models;

namespace Cotton.Server.Extensions
{
    public static class CottonDbContextExtensions
    {
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
