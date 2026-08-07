// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Configuration;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Database
{
    /// <summary>
    /// Entity Framework context for Cotton domain data and encrypted database fields.
    /// </summary>
    public class CottonDbContext : AuditedDbContext
    {
        private readonly IDatabaseFieldProtector? _databaseFieldProtector;

        /// <summary>
        /// Initializes a context for design-time and raw database operations that do not access encrypted fields.
        /// </summary>
        public CottonDbContext(DbContextOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// Initializes a runtime context with database field protection.
        /// </summary>
        public CottonDbContext(
            DbContextOptions options,
            IDatabaseFieldProtector databaseFieldProtector)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(databaseFieldProtector);
            _databaseFieldProtector = databaseFieldProtector;
        }

        /// <summary>
        /// Folder nodes stored by the server.
        /// </summary>
        public DbSet<Node> Nodes => Set<Node>();

        /// <summary>
        /// User accounts stored by the server.
        /// </summary>
        public DbSet<User> Users => Set<User>();

        /// <summary>
        /// Deduplicated storage chunks stored by the server.
        /// </summary>
        public DbSet<Chunk> Chunks => Set<Chunk>();

        /// <summary>
        /// User-owned layout trees stored by the server.
        /// </summary>
        public DbSet<Layout> UserLayouts => Set<Layout>();

        /// <summary>
        /// Visible file entries stored by the server.
        /// </summary>
        public DbSet<NodeFile> NodeFiles => Set<NodeFile>();

        /// <summary>
        /// Recorded server performance benchmarks.
        /// </summary>
        public DbSet<Benchmark> Benchmarks => Set<Benchmark>();

        /// <summary>
        /// Application version tracking rows.
        /// </summary>
        public DbSet<AppVersion> AppVersions => Set<AppVersion>();

        /// <summary>
        /// User notification rows.
        /// </summary>
        public DbSet<Notification> Notifications => Set<Notification>();

        /// <summary>
        /// Immutable file-content manifests.
        /// </summary>
        public DbSet<FileManifest> FileManifests => Set<FileManifest>();

        /// <summary>
        /// Temporary direct-download token rows.
        /// </summary>
        public DbSet<DownloadToken> DownloadTokens => Set<DownloadToken>();

        /// <summary>
        /// Public share-token rows.
        /// </summary>
        public DbSet<NodeShareToken> NodeShareTokens => Set<NodeShareToken>();

        /// <summary>
        /// Chunk ownership rows used for proof-of-ownership checks.
        /// </summary>
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();

        /// <summary>
        /// Registered user passkey credentials.
        /// </summary>
        public DbSet<UserPasskeyCredential> UserPasskeyCredentials => Set<UserPasskeyCredential>();

        /// <summary>
        /// Configured OpenID Connect identity providers.
        /// </summary>
        public DbSet<OidcProvider> OidcProviders => Set<OidcProvider>();

        /// <summary>
        /// External identities linked to Cotton users.
        /// </summary>
        public DbSet<UserExternalIdentity> UserExternalIdentities => Set<UserExternalIdentity>();

        /// <summary>
        /// Short-lived OpenID Connect login states.
        /// </summary>
        public DbSet<OidcLoginState> OidcLoginStates => Set<OidcLoginState>();

        /// <summary>
        /// Ordered manifest-to-chunk mapping rows.
        /// </summary>
        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();

        /// <summary>
        /// Refresh-token session rows.
        /// </summary>
        public DbSet<ExtendedRefreshToken> RefreshTokens => Set<ExtendedRefreshToken>();

        /// <summary>
        /// Server-wide Cotton settings rows.
        /// </summary>
        public DbSet<CottonServerSettings> ServerSettings => Set<CottonServerSettings>();

        /// <summary>
        /// Durable ordered sync-change feed rows.
        /// </summary>
        public DbSet<SyncChange> SyncChanges => Set<SyncChange>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            DatabaseIntegrityModelConfiguration.Configure(modelBuilder);
            EncryptedStringModelConfiguration.Configure(modelBuilder, _databaseFieldProtector);
        }
    }
}
