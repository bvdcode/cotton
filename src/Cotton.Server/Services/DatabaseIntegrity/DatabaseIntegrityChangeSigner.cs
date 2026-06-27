// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Signs pending EF changes for protected entities immediately before they are saved.
    /// </summary>
    /// <remarks>
    /// The signer lives in the database layer boundary rather than in individual handlers so every normal write path is
    /// covered consistently.
    /// </remarks>
    public class DatabaseIntegrityChangeSigner : IDatabaseIntegrityChangeSigner
    {
        private readonly IDatabaseIntegrityProtector _protector;
        private readonly IDatabaseIntegrityDescriptorRegistry _descriptors;
        private readonly IDatabaseIntegrityFailureReporter _failures;
        private readonly DatabaseIntegrityRuntimeOptions _runtimeOptions;

        /// <summary>
        /// Initializes a new save-time integrity signer.
        /// </summary>
        public DatabaseIntegrityChangeSigner(
            IDatabaseIntegrityProtector protector,
            IDatabaseIntegrityDescriptorRegistry descriptors,
            IDatabaseIntegrityFailureReporter failures,
            DatabaseIntegrityRuntimeOptions runtimeOptions)
        {
            _protector = protector;
            _descriptors = descriptors;
            _failures = failures;
            _runtimeOptions = runtimeOptions;
        }

        /// <inheritdoc />
        public void SignPendingChanges(DbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            if (dbContext.ChangeTracker.AutoDetectChangesEnabled)
            {
                dbContext.ChangeTracker.DetectChanges();
            }

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

                // The primary key participates in the signed payload, so signing a temporary EF key would create
                // a MAC that cannot verify after SaveChanges assigns the real key.
                EnsureStablePrimaryKey(entry, descriptor);
                if (entry.State == EntityState.Modified
                    && _runtimeOptions.SaveOriginalStateValidationEnabled)
                {
                    RequireOriginalStateValid(entry, descriptor);
                }

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

        private void RequireOriginalStateValid(EntityEntry entry, IDatabaseIntegrityDescriptor descriptor)
        {
            object? versionValue = entry.Property(DatabaseIntegrityColumns.VersionProperty).OriginalValue;
            object? macValue = entry.Property(DatabaseIntegrityColumns.MacProperty).OriginalValue;
            object originalEntity = entry.OriginalValues.ToObject();
            if (versionValue is int version
                && macValue is byte[] mac
                && DatabaseIntegrityDescriptorVersions.TryVerify(
                    originalEntity,
                    descriptor,
                    version,
                    mac,
                    _protector,
                    out _))
            {
                return;
            }

            string entityKey = descriptor.GetEntityKey(originalEntity);
            _failures.Report(new DatabaseIntegrityFailure(
                descriptor.EntityName,
                entityKey,
                "save.original-state",
                DateTime.UtcNow));
            throw new DatabaseIntegrityException(descriptor.EntityName, entityKey);
        }
    }
}
