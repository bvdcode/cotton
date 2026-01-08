// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Database
{
    public class CottonDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<Node> Nodes => Set<Node>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Chunk> Chunks => Set<Chunk>();
        public DbSet<Layout> UserLayouts => Set<Layout>();
        public DbSet<NodeFile> NodeFiles => Set<NodeFile>();
        public DbSet<Benchmark> Benchmarks => Set<Benchmark>();
        public DbSet<FilePreview> FilePreviews => Set<FilePreview>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<FileManifest> FileManifests => Set<FileManifest>();
        public DbSet<DownloadToken> DownloadTokens => Set<DownloadToken>();
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();
        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();
        public DbSet<CottonServerSettings> ServerSettings => Set<CottonServerSettings>();
    }
}
