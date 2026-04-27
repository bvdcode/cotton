// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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
    public class CottonDbContext(
        DbContextOptions options,
        IStreamCipher? streamCipher = null,
        ILogger<CottonDbContext>? logger = null) : AuditedDbContext(options)
    {
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
        public DbSet<ChunkOwnership> ChunkOwnerships => Set<ChunkOwnership>();
        public DbSet<FileManifestChunk> FileManifestChunks => Set<FileManifestChunk>();
        public DbSet<ExtendedRefreshToken> RefreshTokens => Set<ExtendedRefreshToken>();
        public DbSet<CottonServerSettings> ServerSettings => Set<CottonServerSettings>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
