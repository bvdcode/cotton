// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

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
        public DbSet<Node> Nodes => Set<Node>();
        public DbSet<FileManifest> FileManifests => Set<FileManifest>();
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();
        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();
        public DbSet<NodeFile> NodeFiles => Set<NodeFile>();
    }
}
