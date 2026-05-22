// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityDiagnosticsService(
    CottonDbContext _dbContext,
    IDatabaseIntegrityDescriptorRegistry _descriptors)
{
    private static readonly MethodInfo CountUnsignedRowsCoreMethod = typeof(DatabaseIntegrityDiagnosticsService)
        .GetMethod(
            nameof(CountUnsignedRowsCoreAsync),
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityDiagnosticsService),
            nameof(CountUnsignedRowsCoreAsync));

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
            BridgeBackfillEnabled = true,
            ProtectedEntityTypes = _descriptors.All.Count,
            UnsignedProtectedRows = unsignedRows,
        };
    }

    private async Task<int> CountUnsignedRowsAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
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
