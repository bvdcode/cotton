// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
    private static readonly System.Reflection.MethodInfo MarkAllRowsForResignCoreMethod = typeof(DatabaseIntegrityBridgeBackfillService)
        .GetMethod(
            nameof(MarkAllRowsForResignCoreAsync),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityBridgeBackfillService),
            nameof(MarkAllRowsForResignCoreAsync));
    private static readonly System.Reflection.MethodInfo LoadRowsToSignCoreMethod = typeof(DatabaseIntegrityBridgeBackfillService)
        .GetMethod(
            nameof(LoadRowsToSignCoreAsync),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(DatabaseIntegrityBridgeBackfillService),
            nameof(LoadRowsToSignCoreAsync));

    private readonly IReadOnlyCollection<IDatabaseIntegrityDescriptor> _descriptorsToBackfill = _descriptors.All;

    /// <inheritdoc />
    public async Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        int total = 0;
        foreach (IDatabaseIntegrityDescriptor descriptor in _descriptorsToBackfill)
        {
            await MarkAllRowsForResignAsync(descriptor, cancellationToken);
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

        await transaction.CommitAsync(cancellationToken);
        return total;
    }

    private async Task<int> BackfillEntitySetAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        int signed = 0;
        while (true)
        {
            List<object> rows = await LoadRowsToSignAsync(descriptor, cancellationToken);
            if (rows.Count == 0)
            {
                return signed;
            }

            foreach (object row in rows)
            {
                byte[] mac = _protector.Sign(row, descriptor);
                await UpdateIntegrityMetadataAsync(descriptor, row, mac, cancellationToken);
            }

            signed += rows.Count;
            _dbContext.ChangeTracker.Clear();
        }
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

    private async Task MarkAllRowsForResignAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var genericMethod = MarkAllRowsForResignCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task)genericMethod.Invoke(this, [descriptor, cancellationToken])!;
        await task;
    }

    private async Task<List<object>> LoadRowsToSignAsync(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var genericMethod = LoadRowsToSignCoreMethod.MakeGenericMethod(descriptor.EntityType);
        var task = (Task<List<object>>)genericMethod.Invoke(this, [descriptor, cancellationToken])!;
        return await task;
    }

    private async Task MarkAllRowsForResignCoreAsync<TEntity>(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        IEntityType entityType = _dbContext.Model.FindEntityType(descriptor.EntityType)
            ?? throw new InvalidOperationException($"Protected entity type {descriptor.EntityType.Name} is not mapped.");
        StoreObjectIdentifier storeObject = GetStoreObject(entityType, descriptor);
        string versionColumn = GetColumnName(entityType, DatabaseIntegrityColumns.VersionProperty, storeObject);
        string sql = $"UPDATE {QuoteQualifiedTable(storeObject)} SET {QuoteIdentifier(versionColumn)} = NULL";
        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task<List<object>> LoadRowsToSignCoreAsync<TEntity>(
        IDatabaseIntegrityDescriptor descriptor,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        List<TEntity> rows = await _dbContext.Set<TEntity>()
            .Where(x => EF.Property<byte[]?>(x, DatabaseIntegrityColumns.MacProperty) == null
                || EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) == null
                || EF.Property<int?>(x, DatabaseIntegrityColumns.VersionProperty) != descriptor.SchemaVersion)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        return rows.Cast<object>().ToList();
    }
}
