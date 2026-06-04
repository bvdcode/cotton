// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Verifies protected tracked entities when they cross a security-sensitive read boundary.
/// </summary>
/// <remarks>
/// Save-time signing prevents Cotton from writing unsigned changes. Read-time verification prevents direct database
/// edits from being trusted when authentication, token consumption, or other protected flows use the row.
/// </remarks>
public sealed class DatabaseIntegrityVerifier(
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

        var entry = dbContext.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            throw new InvalidOperationException(
                $"Database integrity verification requires a tracked {descriptor.EntityName} entity.");
        }

        // Integrity metadata is mapped as EF shadow properties. The verifier therefore requires the tracked entry
        // instead of accepting detached DTO-like objects that no longer carry the database MAC/version.
        byte[]? mac = (byte[]?)entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue;
        int? version = (int?)entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue;
        // Existing databases enter this release without row MACs. The startup bridge backfills them before
        // normal traffic, but this read-side allowance keeps older rows from bricking the transition window.
        if (mac is null || version is null)
        {
            _logger.LogDebug(
                "Database integrity metadata is missing for {EntityName} {EntityKey} at {Boundary}; allowing legacy row during the integrity rollout window.",
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
            return;
        }

        if (version != descriptor.SchemaVersion)
        {
            _logger.LogWarning(
                "Database integrity metadata has unsupported schema version {Version} for {EntityName} {EntityKey} at {Boundary}; allowing row during the integrity rollout window.",
                version,
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
            return;
        }

        if (!_protector.Verify(entity, descriptor, mac))
        {
            if (!AllowsMismatchedMacDuringBridge(descriptor))
            {
                Fail(descriptor, entity, boundary);
            }

            _logger.LogWarning(
                "Database integrity verification failed for {EntityName} {EntityKey} at {Boundary}; allowing row during the integrity rollout window.",
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
        }
    }

    private static bool AllowsMismatchedMacDuringBridge(IDatabaseIntegrityDescriptor descriptor) =>
        descriptor.EntityType == typeof(FileManifest);

    [DoesNotReturn]
    private void Fail(
        IDatabaseIntegrityDescriptor descriptor,
        object entity,
        string boundary)
    {
        string entityKey = descriptor.GetEntityKey(entity);
        _failures.Report(new DatabaseIntegrityFailure(
            descriptor.EntityName,
            entityKey,
            boundary,
            DateTime.UtcNow));
        throw new DatabaseIntegrityException(descriptor.EntityName, entityKey);
    }
}
