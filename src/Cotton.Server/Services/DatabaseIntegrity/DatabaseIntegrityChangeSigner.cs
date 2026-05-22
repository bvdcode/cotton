// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityChangeSigner : IDatabaseIntegrityChangeSigner
{
    private readonly IDatabaseIntegrityProtector _protector;
    private readonly IReadOnlyDictionary<Type, IDatabaseIntegrityDescriptor> _descriptors;

    public DatabaseIntegrityChangeSigner(
        IDatabaseIntegrityProtector protector,
        IEnumerable<IDatabaseIntegrityDescriptor> descriptors)
    {
        _protector = protector;
        _descriptors = descriptors.ToDictionary(x => x.EntityType);
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

            if (!TryGetDescriptor(entry.Entity.GetType(), out IDatabaseIntegrityDescriptor? descriptor))
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

    private bool TryGetDescriptor(
        Type entityType,
        out IDatabaseIntegrityDescriptor descriptor)
    {
        if (_descriptors.TryGetValue(entityType, out descriptor!))
        {
            return true;
        }

        foreach ((Type descriptorType, IDatabaseIntegrityDescriptor candidate) in _descriptors)
        {
            if (descriptorType.IsAssignableFrom(entityType))
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = null!;
        return false;
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
