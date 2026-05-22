// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityChangeSigner : IDatabaseIntegrityChangeSigner
{
    private readonly IDatabaseIntegrityProtector _protector;
    private readonly IDatabaseIntegrityDescriptorRegistry _descriptors;

    public DatabaseIntegrityChangeSigner(
        IDatabaseIntegrityProtector protector,
        IDatabaseIntegrityDescriptorRegistry descriptors)
    {
        _protector = protector;
        _descriptors = descriptors;
    }

    public void SignPendingChanges(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        foreach (EntityEntry entry in dbContext.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (!_descriptors.TryGet(entry.Entity.GetType(), out IDatabaseIntegrityDescriptor? descriptor))
            {
                continue;
            }

            if (!HasIntegrityShadowProperties(entry))
            {
                continue;
            }

            EnsureStablePrimaryKey(entry, descriptor);
            byte[] mac = _protector.Sign(entry.Entity, descriptor);
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = descriptor.SchemaVersion;
            entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = mac;
        }
    }

    private static bool HasIntegrityShadowProperties(EntityEntry entry)
    {
        return entry.Metadata.FindProperty(DatabaseIntegrityColumns.VersionProperty) is not null
            && entry.Metadata.FindProperty(DatabaseIntegrityColumns.MacProperty) is not null;
    }

    private static void EnsureStablePrimaryKey(EntityEntry entry, IDatabaseIntegrityDescriptor descriptor)
    {
        PropertyEntry? idProperty = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "Id");
        if (idProperty?.CurrentValue is Guid id && id == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Cannot sign {descriptor.EntityName} before EF assigns a stable primary key.");
        }

        if (idProperty?.IsTemporary == true)
        {
            throw new InvalidOperationException(
                $"Cannot sign {descriptor.EntityName} while its primary key is temporary.");
        }
    }
}
