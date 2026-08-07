// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cotton.Database.Integrity
{
    internal static class DatabaseIntegrityModelConfiguration
    {
        private static readonly Type[] ProtectedEntityTypes =
        [
            typeof(User),
            typeof(UserPasskeyCredential),
            typeof(OidcProvider),
            typeof(UserExternalIdentity),
            typeof(OidcLoginState),
            typeof(ExtendedRefreshToken),
            typeof(DownloadToken),
            typeof(NodeShareToken),
            typeof(CottonServerSettings),
            typeof(Node),
            typeof(NodeFile),
            typeof(FileManifest),
            typeof(FileManifestChunk),
            typeof(Chunk)
        ];

        public static void Configure(ModelBuilder modelBuilder)
        {
            foreach (Type entityType in ProtectedEntityTypes)
            {
                EntityTypeBuilder entity = modelBuilder.Entity(entityType);
                entity.Property<int?>(DatabaseIntegrityColumns.VersionProperty)
                    .HasColumnName(DatabaseIntegrityColumns.VersionColumn);
                entity.Property<byte[]?>(DatabaseIntegrityColumns.MacProperty)
                    .HasColumnName(DatabaseIntegrityColumns.MacColumn)
                    .IsConcurrencyToken();
            }
        }
    }
}
