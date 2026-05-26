// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Database.Models.Attributes;
using EasyExtensions.Abstractions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;

namespace Cotton.Database
{
    /// <summary>Entity Framework context for Cotton domain data and encrypted database fields.</summary>
    public class CottonDbContext(
        DbContextOptions options,
        IStreamCipher? streamCipher = null,
        ILogger<CottonDbContext>? logger = null,
        IDatabaseIntegrityChangeSigner? integrityChangeSigner = null) : AuditedDbContext(options)
    {
        /// <summary>Folder nodes stored by the server.</summary>
        public DbSet<Node> Nodes => Set<Node>();
        /// <summary>User accounts stored by the server.</summary>
        public DbSet<User> Users => Set<User>();
        /// <summary>Deduplicated storage chunks stored by the server.</summary>
        public DbSet<Chunk> Chunks => Set<Chunk>();
        /// <summary>User-owned layout trees stored by the server.</summary>
        public DbSet<Layout> UserLayouts => Set<Layout>();
        /// <summary>Visible file entries stored by the server.</summary>
        public DbSet<NodeFile> NodeFiles => Set<NodeFile>();
        /// <summary>Recorded server performance benchmarks.</summary>
        public DbSet<Benchmark> Benchmarks => Set<Benchmark>();
        /// <summary>Application version tracking rows.</summary>
        public DbSet<AppVersion> AppVersions => Set<AppVersion>();
        /// <summary>User notification rows.</summary>
        public DbSet<Notification> Notifications => Set<Notification>();
        /// <summary>Immutable file-content manifests.</summary>
        public DbSet<FileManifest> FileManifests => Set<FileManifest>();
        /// <summary>Temporary direct-download token rows.</summary>
        public DbSet<DownloadToken> DownloadTokens => Set<DownloadToken>();
        /// <summary>Public share-token rows.</summary>
        public DbSet<NodeShareToken> NodeShareTokens => Set<NodeShareToken>();
        /// <summary>Chunk ownership rows used for proof-of-ownership checks.</summary>
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();
        /// <summary>Registered user passkey credentials.</summary>
        public DbSet<UserPasskeyCredential> UserPasskeyCredentials => Set<UserPasskeyCredential>();
        /// <summary>Configured OpenID Connect identity providers.</summary>
        public DbSet<OidcProvider> OidcProviders => Set<OidcProvider>();
        /// <summary>External identities linked to Cotton users.</summary>
        public DbSet<UserExternalIdentity> UserExternalIdentities => Set<UserExternalIdentity>();
        /// <summary>Short-lived OpenID Connect login states.</summary>
        public DbSet<OidcLoginState> OidcLoginStates => Set<OidcLoginState>();
        /// <summary>Ordered manifest-to-chunk mapping rows.</summary>
        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();
        /// <summary>Refresh-token session rows.</summary>
        public DbSet<ExtendedRefreshToken> RefreshTokens => Set<ExtendedRefreshToken>();
        /// <summary>Server-wide Cotton settings rows.</summary>
        public DbSet<CottonServerSettings> ServerSettings => Set<CottonServerSettings>();

        /// <summary>Signs pending protected rows before saving changes.</summary>
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            integrityChangeSigner?.SignPendingChanges(this);
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        /// <summary>Signs pending protected rows before saving changes.</summary>
        public override int SaveChanges()
        {
            integrityChangeSigner?.SignPendingChanges(this);
            return base.SaveChanges();
        }

        /// <summary>Signs pending protected rows before saving changes asynchronously.</summary>
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            integrityChangeSigner?.SignPendingChanges(this);
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>Signs pending protected rows before saving changes asynchronously.</summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            integrityChangeSigner?.SignPendingChanges(this);
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureIntegrityShadowProperties<User>(modelBuilder);
            ConfigureIntegrityShadowProperties<UserPasskeyCredential>(modelBuilder);
            ConfigureIntegrityShadowProperties<OidcProvider>(modelBuilder);
            ConfigureIntegrityShadowProperties<UserExternalIdentity>(modelBuilder);
            ConfigureIntegrityShadowProperties<OidcLoginState>(modelBuilder);
            ConfigureIntegrityShadowProperties<ExtendedRefreshToken>(modelBuilder);
            ConfigureIntegrityShadowProperties<DownloadToken>(modelBuilder);
            ConfigureIntegrityShadowProperties<NodeShareToken>(modelBuilder);
            ConfigureIntegrityShadowProperties<CottonServerSettings>(modelBuilder);
            ConfigureIntegrityShadowProperties<Node>(modelBuilder);
            ConfigureIntegrityShadowProperties<NodeFile>(modelBuilder);
            ConfigureIntegrityShadowProperties<FileManifest>(modelBuilder);
            ConfigureIntegrityShadowProperties<FileManifestChunk>(modelBuilder);
            ConfigureIntegrityShadowProperties<Chunk>(modelBuilder);

            modelBuilder.Entity<FileManifest>()
                .Property(x => x.PreviewGeneratorVersion)
                .HasDefaultValue(0);

            ValueConverter<string?, string?> encryptedStringConverter = new(
                value => EncryptString(value),
                value => DecryptString(value));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (clrType is null)
                {
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    var propertyInfo = property.PropertyInfo;
                    if (propertyInfo is null)
                    {
                        continue;
                    }

                    bool hasEncryptedAttribute = Attribute.IsDefined(propertyInfo, typeof(EncryptedAttribute));
                    if (!hasEncryptedAttribute || property.ClrType != typeof(string))
                    {
                        continue;
                    }

                    modelBuilder.Entity(clrType)
                        .Property(propertyInfo.Name)
                        .HasConversion(encryptedStringConverter);
                }
            }
        }

        private static void ConfigureIntegrityShadowProperties<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class
        {
            modelBuilder.Entity<TEntity>()
                .Property<int?>(DatabaseIntegrityColumns.VersionProperty)
                .HasColumnName(DatabaseIntegrityColumns.VersionColumn);

            modelBuilder.Entity<TEntity>()
                .Property<byte[]?>(DatabaseIntegrityColumns.MacProperty)
                .HasColumnName(DatabaseIntegrityColumns.MacColumn);
        }

        private string? EncryptString(string? value)
        {
            if (value is null || streamCipher is null)
            {
                return value;
            }

            byte[] encryptedBytes = streamCipher.EncryptString(value);
            return Convert.ToBase64String(encryptedBytes);
        }

        private string? DecryptString(string? value)
        {
            if (value is null || streamCipher is null)
            {
                return value;
            }

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(value);
                return streamCipher.DecryptString(encryptedBytes);
            }
            catch
            {
                logger?.LogWarning(
                    "Failed to decrypt value in encrypted EF converter. Falling back to raw database value.");
                return value;
            }
        }
    }
}
