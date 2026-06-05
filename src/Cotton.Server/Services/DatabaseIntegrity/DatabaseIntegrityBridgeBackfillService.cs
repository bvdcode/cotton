// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using System.Diagnostics;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Startup bridge that re-signs protected rows from the current database state.
/// </summary>
/// <remarks>
/// This is an operational recovery bridge: it treats the current database contents as canonical and refreshes row MACs
/// so installations can recover from descriptor-shape mistakes and interrupted integrity rollouts.
/// </remarks>
public sealed class DatabaseIntegrityBridgeBackfillService(
    CottonDbContext _dbContext,
    IDatabaseIntegrityProtector _protector,
    IDatabaseIntegrityDescriptorRegistry _descriptors,
    ILogger<DatabaseIntegrityBridgeBackfillService> _logger) : IDatabaseIntegrityBridgeBackfillService
{
    private const int BatchSize = 250;
    private const int ProgressLogRowInterval = 10_000;
    private static readonly TimeSpan InterBatchDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ProgressLogInterval = TimeSpan.FromSeconds(15);
    private static readonly System.Reflection.MethodInfo BackfillEntitySetCoreMethod = typeof(DatabaseIntegrityBridgeBackfillService)
        .GetMethod(
            nameof(BackfillEntitySetCoreAsync),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityBridgeBackfillService),
            nameof(BackfillEntitySetCoreAsync));

    private readonly IReadOnlyCollection<IDatabaseIntegrityDescriptor> _descriptorsToBackfill = _descriptors.All;

    /// <inheritdoc />
    public async Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Database integrity bridge full backfill started; entity sets {EntitySetCount}, batch size {BatchSize}, inter-batch delay {InterBatchDelay}.",
            _descriptorsToBackfill.Count,
            BatchSize,
            InterBatchDelay);

        int total = 0;
        foreach (IDatabaseIntegrityDescriptor descriptor in _descriptorsToBackfill)
        {
            int count = await BackfillEntitySetAsync(descriptor, cancellationToken);
            total += count;
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Database integrity bridge full backfill finished; signed {SignedRows} rows across {EntitySetCount} entity sets in {Elapsed}.",
            total,
            _descriptorsToBackfill.Count,
            stopwatch.Elapsed);

        return total;
    }

    private async Task<int> BackfillEntitySetAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var genericMethod = BackfillEntitySetCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task<int>)genericMethod.Invoke(this, [descriptor, cancellationToken])!;
        return await task;
    }

    private async Task UpdateIntegrityMetadataAsync(
        IDatabaseIntegrityDescriptor descriptor,
        object row,
        byte[] mac,
        CancellationToken cancellationToken)
    {
        IEntityType entityType = _dbContext.Model.FindEntityType(descriptor.EntityType)
            ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityType.Name} is not mapped.");
        IKey primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityName} has no primary key.");
        StoreObjectIdentifier storeObject = GetStoreObject(entityType, descriptor);
        string versionColumn = GetColumnName(entityType, DatabaseIntegrityColumns.VersionProperty, storeObject);
        string macColumn = GetColumnName(entityType, DatabaseIntegrityColumns.MacProperty, storeObject);

        var entry = _dbContext.Entry(row);
        List<object> parameters = [descriptor.SchemaVersion, mac];
        List<string> keyPredicates = new(primaryKey.Properties.Count);
        for (int index = 0; index < primaryKey.Properties.Count; index++)
        {
            IProperty property = primaryKey.Properties[index];
            string keyColumn = GetColumnName(property, storeObject);
            object keyValue = entry.Property(property.Name).CurrentValue
                ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityName} has a null primary key value.");
            parameters.Add(keyValue);
            keyPredicates.Add($"{QuoteIdentifier(keyColumn)} = {{{index + 2}}}");
        }

        string sql =
            $"UPDATE {QuoteQualifiedTable(storeObject)} " +
            $"SET {QuoteIdentifier(versionColumn)} = {{0}}, {QuoteIdentifier(macColumn)} = {{1}} " +
            $"WHERE {string.Join(" AND ", keyPredicates)}";
        int updatedRows = await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        if (updatedRows != 1)
        {
            throw new InvalidOperationException($"Expected to update one {descriptor.EntityName} integrity row, but updated {updatedRows}.");
        }
    }

    private static StoreObjectIdentifier GetStoreObject(IEntityType entityType, IDatabaseIntegrityDescriptor descriptor)
    {
        string tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityName} is not mapped to a table.");
        return StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
    }

    private static string GetColumnName(
        IEntityType entityType,
        string propertyName,
        StoreObjectIdentifier storeObject)
    {
        IProperty property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"Protected entity type {entityType.ClrType.Name} is missing {propertyName}.");
        return GetColumnName(property, storeObject);
    }

    private static string GetColumnName(IProperty property, StoreObjectIdentifier storeObject)
    {
        return property.GetColumnName(storeObject)
            ?? throw new InvalidOperationException($"Property {property.Name} is not mapped to a column.");
    }

    private static string QuoteQualifiedTable(StoreObjectIdentifier storeObject)
    {
        return string.IsNullOrEmpty(storeObject.Schema)
            ? QuoteIdentifier(storeObject.Name)
            : $"{QuoteIdentifier(storeObject.Schema)}.{QuoteIdentifier(storeObject.Name)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private async Task<int> BackfillEntitySetCoreAsync<TEntity>(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        IEntityType entityType = _dbContext.Model.FindEntityType(descriptor.EntityType)
            ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityType.Name} is not mapped.");
        IKey primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityName} has no primary key.");
        if (primaryKey.Properties.Count != 1)
        {
            throw new InvalidOperationException($"Protected entity type {descriptor.EntityName} must have a single-column primary key for bridge backfill.");
        }

        StoreObjectIdentifier storeObject = GetStoreObject(entityType, descriptor);
        IProperty keyProperty = primaryKey.Properties[0];
        string keyColumn = GetColumnName(keyProperty, storeObject);
        string table = QuoteQualifiedTable(storeObject);

        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Database integrity bridge backfill started for {EntityName} table {Table}; key column {KeyColumn}, schema version {SchemaVersion}.",
            descriptor.EntityName,
            table,
            keyColumn,
            descriptor.SchemaVersion);

        int signed = 0;
        int nextProgressRowCount = ProgressLogRowInterval;
        TimeSpan lastProgressLogAt = TimeSpan.Zero;
        object? lastKey = null;
        while (true)
        {
            List<TEntity> rows = await LoadNextBatchAsync<TEntity>(
                storeObject,
                keyColumn,
                lastKey,
                cancellationToken);
            if (rows.Count == 0)
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "Database integrity bridge backfill finished for {EntityName} table {Table}; signed {SignedRows} rows in {Elapsed}.",
                    descriptor.EntityName,
                    table,
                    signed,
                    stopwatch.Elapsed);
                return signed;
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            foreach (TEntity row in rows)
            {
                byte[] mac = _protector.Sign(row, descriptor);
                await UpdateIntegrityMetadataAsync(descriptor, row, mac, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            lastKey = _dbContext.Entry(rows[^1]).Property(keyProperty.Name).CurrentValue
                ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityName} has a null primary key value.");
            signed += rows.Count;
            _dbContext.ChangeTracker.Clear();

            if (ShouldLogProgress(signed, stopwatch.Elapsed, ref nextProgressRowCount, ref lastProgressLogAt))
            {
                _logger.LogInformation(
                    "Database integrity bridge backfill progress for {EntityName} table {Table}; signed {SignedRows} rows so far in {Elapsed}.",
                    descriptor.EntityName,
                    table,
                    signed,
                    stopwatch.Elapsed);
            }

            await Task.Delay(InterBatchDelay, cancellationToken);
        }
    }

    private static bool ShouldLogProgress(
        int signed,
        TimeSpan elapsed,
        ref int nextProgressRowCount,
        ref TimeSpan lastProgressLogAt)
    {
        if (signed >= nextProgressRowCount)
        {
            while (nextProgressRowCount <= signed)
            {
                nextProgressRowCount += ProgressLogRowInterval;
            }

            lastProgressLogAt = elapsed;
            return true;
        }

        if (elapsed - lastProgressLogAt < ProgressLogInterval)
        {
            return false;
        }

        lastProgressLogAt = elapsed;
        return true;
    }

    private async Task<List<TEntity>> LoadNextBatchAsync<TEntity>(
        StoreObjectIdentifier storeObject,
        string keyColumn,
        object? lastKey,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        string table = QuoteQualifiedTable(storeObject);
        string quotedKey = QuoteIdentifier(keyColumn);
        string sql = lastKey is null
            ? $"SELECT * FROM {table} ORDER BY {quotedKey} LIMIT {BatchSize}"
            : $"SELECT * FROM {table} WHERE {quotedKey} > {{0}} ORDER BY {quotedKey} LIMIT {BatchSize}";

        return lastKey is null
            ? await _dbContext.Set<TEntity>().FromSqlRaw(sql).ToListAsync(cancellationToken)
            : await _dbContext.Set<TEntity>().FromSqlRaw(sql, lastKey).ToListAsync(cancellationToken);
    }
}
