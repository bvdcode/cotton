using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Cotton.Server.Database
{
    public class CottonDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Chunk> Chunks => Set<Chunk>();
        public DbSet<Layout> UserLayouts => Set<Layout>();
        public DbSet<FileManifest> FileManifests => Set<FileManifest>();
        public DbSet<Node> UserLayoutNodes => Set<Node>();
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();
        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();
        public DbSet<UserLayoutNodeFile> UserLayoutNodeFiles => Set<UserLayoutNodeFile>();
    }
}
