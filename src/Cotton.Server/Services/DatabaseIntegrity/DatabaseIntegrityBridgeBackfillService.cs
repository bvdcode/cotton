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
    private static readonly MethodInfo CountMissingRowsCoreMethod = typeof(DatabaseIntegrityBridgeBackfillService)
        .GetMethod(
            nameof(CountMissingRowsCoreAsync),
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityBridgeBackfillService),
            nameof(CountMissingRowsCoreAsync));
    private static readonly MethodInfo CountRowsWithAnyIntegrityMetadataCoreMethod = typeof(DatabaseIntegrityBridgeBackfillService)
        .GetMethod(
            nameof(CountRowsWithAnyIntegrityMetadataCoreAsync),
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityBridgeBackfillService),
            nameof(CountRowsWithAnyIntegrityMetadataCoreAsync));
    private static readonly MethodInfo CountStaleRowsCoreMethod = typeof(DatabaseIntegrityBridgeBackfillService)
        .GetMethod(
            nameof(CountStaleRowsCoreAsync),
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityBridgeBackfillService),
            nameof(CountStaleRowsCoreAsync));
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
        BridgeState state = await LoadBridgeStateAsync(cancellationToken);
        if (state.MissingRows == 0 && state.StaleRows == 0)
        {
            return 0;
        }

        if (state.StaleRows > 0)
        {
            throw new InvalidOperationException(
                "Database integrity bridge found protected rows with an unsupported integrity schema version. " +
                "Refusing to re-sign existing data because that could bless database tampering.");
        }

        if (state.MissingRows > 0 && state.RowsWithAnyIntegrityMetadata > 0)
        {
            throw new InvalidOperationException(
                "Database integrity bridge found protected rows without integrity metadata after integrity metadata already exists. " +
                "Refusing to sign existing data because that could bless database tampering.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
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

        await transaction.CommitAsync(cancellationToken);
        return total;
    }

    private async Task<BridgeState> LoadBridgeStateAsync(CancellationToken cancellationToken)
    {
        int missingRows = 0;
        int staleRows = 0;
        int rowsWithAnyIntegrityMetadata = 0;
        foreach (IDatabaseIntegrityDescriptor descriptor in _phaseOneDescriptors)
        {
            missingRows += await CountMissingRowsAsync(descriptor, cancellationToken);
            staleRows += await CountStaleRowsAsync(descriptor, cancellationToken);
            rowsWithAnyIntegrityMetadata += await CountRowsWithAnyIntegrityMetadataAsync(descriptor, cancellationToken);
        }

        return new BridgeState(missingRows, staleRows, rowsWithAnyIntegrityMetadata);
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

    private async Task<int> CountMissingRowsAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var genericMethod = CountMissingRowsCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task<int>)genericMethod.Invoke(this, [cancellationToken])!;
        return await task;
    }

    private async Task<int> CountRowsWithAnyIntegrityMetadataAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var genericMethod = CountRowsWithAnyIntegrityMetadataCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task<int>)genericMethod.Invoke(this, [cancellationToken])!;
        return await task;
    }

    private async Task<int> CountStaleRowsAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var genericMethod = CountStaleRowsCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task<int>)genericMethod.Invoke(this, [descriptor, cancellationToken])!;
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

    private Task<int> CountMissingRowsCoreAsync<TEntity>(
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return _dbContext.Set<TEntity>()
            .CountAsync(x => EF.Property<byte[]?>(x, DatabaseIntegrityColumns.MacProperty) == null
                || EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) == null,
                cancellationToken);
    }

    private Task<int> CountStaleRowsCoreAsync<TEntity>(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return _dbContext.Set<TEntity>()
            .CountAsync(x => EF.Property<byte[]?>(x, DatabaseIntegrityColumns.MacProperty) != null
                && EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) != null
                && EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) != descriptor.SchemaVersion,
                cancellationToken);
    }

    private Task<int> CountRowsWithAnyIntegrityMetadataCoreAsync<TEntity>(
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return _dbContext.Set<TEntity>()
            .CountAsync(x => EF.Property<byte[]?>(x, DatabaseIntegrityColumns.MacProperty) != null
                || EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) != null,
                cancellationToken);
    }

    private readonly record struct BridgeState(
        int MissingRows,
        int StaleRows,
        int RowsWithAnyIntegrityMetadata);
}
