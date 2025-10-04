using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Cotton.Server.Database
{
    public class CottonDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<Blob> Blobs => Set<Blob>();
        public DbSet<Chunk> Chunks => Set<Chunk>();
        public DbSet<BlobChunk> BlobChunks => Set<BlobChunk>();
    }
}
