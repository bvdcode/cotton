// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Configuration;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cotton.Database
{
    public partial class CottonDbContext : AuditedDbContext
    {
        private readonly IDatabaseFieldProtector? _databaseFieldProtector;

        internal IDatabaseFieldProtector? DatabaseFieldProtector => _databaseFieldProtector;

        public CottonDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public CottonDbContext(
            DbContextOptions options,
            IDatabaseFieldProtector databaseFieldProtector)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(databaseFieldProtector);
            _databaseFieldProtector = databaseFieldProtector;
        }

        public DbSet<Node> Nodes => Set<Node>();

        public DbSet<User> Users => Set<User>();

        public DbSet<Chunk> Chunks => Set<Chunk>();

        public DbSet<Layout> UserLayouts => Set<Layout>();

        public DbSet<NodeFile> NodeFiles => Set<NodeFile>();

        public DbSet<Benchmark> Benchmarks => Set<Benchmark>();

        public DbSet<AppVersion> AppVersions => Set<AppVersion>();

        public DbSet<Notification> Notifications => Set<Notification>();

        public DbSet<FileManifest> FileManifests => Set<FileManifest>();

        public DbSet<DownloadToken> DownloadTokens => Set<DownloadToken>();

        public DbSet<NodeShareToken> NodeShareTokens => Set<NodeShareToken>();

        /// <summary>
        /// Chunk ownership rows used for proof-of-ownership checks.
        /// </summary>
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();

        public DbSet<UserPasskeyCredential> UserPasskeyCredentials => Set<UserPasskeyCredential>();

        public DbSet<OidcProvider> OidcProviders => Set<OidcProvider>();

        public DbSet<UserExternalIdentity> UserExternalIdentities => Set<UserExternalIdentity>();

        public DbSet<OidcLoginState> OidcLoginStates => Set<OidcLoginState>();

        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();

        public DbSet<ExtendedRefreshToken> RefreshTokens => Set<ExtendedRefreshToken>();

        public DbSet<CottonServerSettings> ServerSettings => Set<CottonServerSettings>();

        public DbSet<SyncChange> SyncChanges => Set<SyncChange>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, CottonModelCacheKeyFactory>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            DatabaseIntegrityModelConfiguration.Configure(modelBuilder);
            EncryptedStringModelConfiguration.Configure(modelBuilder, _databaseFieldProtector);
        }
    }
}
