// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Builds the security check-up snapshot for database integrity coverage.
/// </summary>
/// <remarks>
/// The diagnostics path intentionally counts metadata state instead of recomputing every row MAC. Large folders and
/// administrative screens can contain tens of thousands of rows; integrity verification stays on security-sensitive
/// read boundaries where the application is about to trust a protected row.
/// </remarks>
public sealed class DatabaseIntegrityDiagnosticsService(
    CottonDbContext _dbContext,
    IDatabaseIntegrityDescriptorRegistry _descriptors)
{
    // Descriptors are discovered at runtime, while EF's Set<TEntity>() API is generic. Reflection is contained in this
    // adapter so descriptors can remain simple policy objects without knowing about DbSet plumbing.
    private static readonly MethodInfo CountUnsignedRowsCoreMethod = typeof(DatabaseIntegrityDiagnosticsService)
        .GetMethod(
            nameof(CountUnsignedRowsCoreAsync),
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityDiagnosticsService),
            nameof(CountUnsignedRowsCoreAsync));

    /// <summary>Returns counts of protected rows, missing metadata, and unsupported integrity versions.</summary>
    public async Task<DatabaseIntegrityDiagnosticsDto> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        int unsignedRows = 0;
        foreach (IDatabaseIntegrityDescriptor descriptor in _descriptors.All)
        {
            unsignedRows += await CountUnsignedRowsAsync(descriptor, cancellationToken);
        }

        return new DatabaseIntegrityDiagnosticsDto
        {
            Enabled = true,
            ProtectedEntityTypes = _descriptors.All.Count,
            UnsignedProtectedRows = unsignedRows,
        };
    }

    private async Task<int> CountUnsignedRowsAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        // Keep this query on the database side: we only need counts for the check-up score, not entity materialization
        // or cryptographic verification.
        var genericMethod = CountUnsignedRowsCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task<int>)genericMethod.Invoke(this, [descriptor, cancellationToken])!;
        return await task;
    }

    private Task<int> CountUnsignedRowsCoreAsync<TEntity>(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return _dbContext.Set<TEntity>()
            .CountAsync(x => EF.Property<byte[]?>(x, DatabaseIntegrityColumns.MacProperty) == null
                || EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) != descriptor.SchemaVersion,
                cancellationToken);
    }
}
