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

            object? versionValue = entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue;
            object? macValue = entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue;
            if (versionValue is not int version
                || version != descriptor.SchemaVersion
                || macValue is not byte[] mac
                || !_protector.Verify(entity, descriptor, mac))
            {
                ReportFailure(descriptor, entity, boundary);
                _logger.LogError(
                    "Database integrity verification failed for {EntityName} {EntityKey} at {Boundary}.",
                    descriptor.EntityName,
                    descriptor.GetEntityKey(entity),
                    boundary);
                throw new DatabaseIntegrityException(descriptor.EntityName, descriptor.GetEntityKey(entity));
            }
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
