// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

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
    private static readonly TimeSpan InterBatchDelay = TimeSpan.FromMilliseconds(50);
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
        int total = 0;
        foreach (IDatabaseIntegrityDescriptor descriptor in _descriptorsToBackfill)
        {
            int count = await BackfillEntitySetAsync(descriptor, cancellationToken);
            if (count == 0)
            {
                continue;
            }

            total += count;
            _logger.LogWarning(
                "Database integrity bridge re-signed {Count} existing {EntityName} rows from current database state.",
                count,
                descriptor.EntityName);
        }

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

        int signed = 0;
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
            await Task.Delay(InterBatchDelay, cancellationToken);
        }
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
