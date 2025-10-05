using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Cotton.Server.Database
{
    public class CottonDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Blob> Blobs => Set<Blob>();
        public DbSet<Chunk> Chunks => Set<Chunk>();
        public DbSet<BlobChunk> BlobChunks => Set<BlobChunk>();
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();
    }
}
