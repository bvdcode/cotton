// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityVerifier(
    IDatabaseIntegrityProtector _protector,
    IDatabaseIntegrityDescriptorRegistry _descriptors,
    ILogger<DatabaseIntegrityVerifier> _logger) : IDatabaseIntegrityVerifier
{
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

        var entry = dbContext.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            throw new InvalidOperationException(
                $"Database integrity verification requires a tracked {descriptor.EntityName} entity.");
        }

        byte[]? mac = (byte[]?)entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue;
        int? version = (int?)entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue;
        if (mac is null || version != descriptor.SchemaVersion)
        {
            _logger.LogError(
                "Database integrity metadata is missing or stale for {EntityName} {EntityKey} at {Boundary}.",
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
            throw new DatabaseIntegrityException(descriptor.EntityName, descriptor.GetEntityKey(entity));
        }

        if (!_protector.Verify(entity, descriptor, mac))
        {
            _logger.LogError(
                "Database integrity verification failed for {EntityName} {EntityKey} at {Boundary}.",
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
            throw new DatabaseIntegrityException(descriptor.EntityName, descriptor.GetEntityKey(entity));
        }
    }
}
