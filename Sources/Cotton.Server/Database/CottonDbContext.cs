using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Cotton.Server.Database
{
    public class CottonDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Chunk> Chunks => Set<Chunk>();
        public DbSet<UserLayout> UserLayouts => Set<UserLayout>();
        public DbSet<FileManifest> FileManifests => Set<FileManifest>();
        public DbSet<UserLayoutNode> UserLayoutNodes => Set<UserLayoutNode>();
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();
        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();
        public DbSet<UserLayoutNodeFile> UserLayoutNodeFiles => Set<UserLayoutNodeFile>();
    }
}
