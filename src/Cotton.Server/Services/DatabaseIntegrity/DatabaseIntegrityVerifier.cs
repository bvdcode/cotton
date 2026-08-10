// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Verifies protected entities at security-sensitive read boundaries.
    /// </summary>
    public class DatabaseIntegrityVerifier(
        IDatabaseIntegrityProtector _protector,
        IDatabaseIntegrityDescriptorRegistry _descriptors,
        IDatabaseIntegrityFailureReporter _failures,
        ILogger<DatabaseIntegrityVerifier> _logger) : IDatabaseIntegrityVerifier
    {
        /// <inheritdoc />
        public void RequireValid<TEntity>(
            CottonDbContext dbContext,
            TEntity entity,
            string boundary)
            where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentException.ThrowIfNullOrWhiteSpace(boundary);

            if (!_descriptors.TryGet(entity.GetType(), out IDatabaseIntegrityDescriptor descriptor))
            {
                return;
            }

            EntityEntry<TEntity> entry = dbContext.Entry(entity);
            EnsureVerifiableEntry(entry, descriptor, boundary);
            (int version, byte[] mac) = RequireSignature(entry, descriptor, entity, boundary);
            if (version == descriptor.SchemaVersion && _protector.Verify(entity, descriptor, mac))
            {
                return;
            }

            ReportFailure(descriptor, entity, boundary);
            _logger.LogError(
                "Database integrity verification failed for {EntityName} {EntityKey} at {Boundary}.",
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
            throw new DatabaseIntegrityException(descriptor.EntityName, descriptor.GetEntityKey(entity));
        }

        private static void EnsureVerifiableEntry<TEntity>(
            EntityEntry<TEntity> entry,
            IDatabaseIntegrityDescriptor descriptor,
            string boundary)
            where TEntity : class
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            {
                throw new InvalidOperationException(
                    $"Cannot verify detached protected entity {descriptor.EntityName} at {boundary}.");
            }

            if (entry.Metadata.FindProperty(DatabaseIntegrityColumns.VersionProperty) is null
                || entry.Metadata.FindProperty(DatabaseIntegrityColumns.MacProperty) is null)
            {
                throw new InvalidOperationException(
                    $"Protected entity {descriptor.EntityName} is missing integrity shadow properties.");
            }
        }

        private (int Version, byte[] Mac) RequireSignature<TEntity>(
            EntityEntry<TEntity> entry,
            IDatabaseIntegrityDescriptor descriptor,
            TEntity entity,
            string boundary)
            where TEntity : class
        {
            object? versionValue = entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue;
            object? macValue = entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue;
            if (versionValue is int version && macValue is byte[] mac)
            {
                return (version, mac);
            }

            ReportFailure(descriptor, entity, boundary);
            _logger.LogError(
                "Database integrity signature is missing for {EntityName} {EntityKey} at {Boundary}.",
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
#pragma warning disable CS0618 // OBSOLETE TRANSITION: preserve a precise unsigned-row upgrade error during the 0.5 cutover.
            throw new DatabaseIntegritySignatureMissingException(
                descriptor.EntityName,
                descriptor.GetEntityKey(entity));
#pragma warning restore CS0618
        }

        private void ReportFailure<TEntity>(
            IDatabaseIntegrityDescriptor descriptor,
            TEntity entity,
            string boundary)
            where TEntity : class
        {
            _failures.Report(new DatabaseIntegrityFailure(
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary,
                DateTime.UtcNow));
        }
    }
}
