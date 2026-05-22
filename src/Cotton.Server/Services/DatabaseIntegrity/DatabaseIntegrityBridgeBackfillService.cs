// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Cotton.Server.Services.DatabaseIntegrity;

// Temporary bridge for the first integrity release: it blesses rows that existed before
// integrity columns were added. A later strict release must remove this path and
// treat missing MAC/version on protected rows as tampering.
public sealed class DatabaseIntegrityBridgeBackfillService(
    CottonDbContext _dbContext,
    IDatabaseIntegrityProtector _protector,
    IDatabaseIntegrityDescriptorRegistry _descriptors,
    ILogger<DatabaseIntegrityBridgeBackfillService> _logger) : IDatabaseIntegrityBridgeBackfillService
{
    private const int BatchSize = 250;
    private static readonly MethodInfo LoadUnsignedBatchCoreMethod = typeof(DatabaseIntegrityBridgeBackfillService)
        .GetMethod(
            nameof(LoadUnsignedBatchCoreAsync),
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityBridgeBackfillService),
            nameof(LoadUnsignedBatchCoreAsync));

    private readonly IReadOnlyCollection<IDatabaseIntegrityDescriptor> _phaseOneDescriptors = _descriptors.All;

    public async Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken)
    {
        int total = 0;
        foreach (IDatabaseIntegrityDescriptor descriptor in _phaseOneDescriptors)
        {
            int count = await BackfillEntitySetAsync(descriptor, cancellationToken);
            if (count == 0)
            {
                continue;
            }

            total += count;
            _logger.LogWarning(
                "Database integrity bridge signed {Count} existing {EntityName} rows. " +
                "This compatibility bridge is temporary and must be removed after the required upgrade window.",
                count,
                descriptor.EntityName);
        }

        return total;
    }

    private async Task<int> BackfillEntitySetAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        int signed = 0;
        while (true)
        {
            List<object> rows = await LoadUnsignedBatchAsync(descriptor, cancellationToken);
            if (rows.Count == 0)
            {
                return signed;
            }

            foreach (object row in rows)
            {
                var entry = _dbContext.Entry(row);
                entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = descriptor.SchemaVersion;
                entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = _protector.Sign(row, descriptor);
            }

            signed += rows.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }
    }

    private async Task<List<object>> LoadUnsignedBatchAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var genericMethod = LoadUnsignedBatchCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task<List<object>>)genericMethod.Invoke(this, [descriptor, cancellationToken])!;
        return await task;
    }

    private async Task<List<object>> LoadUnsignedBatchCoreAsync<TEntity>(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        List<TEntity> rows = await _dbContext.Set<TEntity>()
            .Where(x => EF.Property<byte[]?>(x, DatabaseIntegrityColumns.MacProperty) == null
                || EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) != descriptor.SchemaVersion)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        return rows.Cast<object>().ToList();
    }
}
