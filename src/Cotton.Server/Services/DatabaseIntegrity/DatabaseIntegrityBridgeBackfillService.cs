// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// One-release bridge that signs protected rows created before integrity metadata existed.
/// </summary>
/// <remarks>
/// This bridge signs rows that predate integrity metadata and any later legacy row that is still unsigned during the
/// current rollout window. Existing metadata is never ignored: unsupported schema versions still stop startup rather than
/// being re-signed blindly.
/// </remarks>
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

    /// <inheritdoc />
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
                "This migration bridge is temporary and must be removed after the required upgrade window.",
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
        foreach (IDatabaseIntegrityDescriptor descriptor in _phaseOneDescriptors)
        {
            missingRows += await CountMissingRowsAsync(descriptor, cancellationToken);
            staleRows += await CountStaleRowsAsync(descriptor, cancellationToken);
        }

        return new BridgeState(missingRows, staleRows);
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
        // Transitional rollout bridge: existing installations started without integrity metadata.
        // Startup signs missing rows only; stale non-null schema versions stay hard failures so
        // Cotton never silently blesses data whose signed payload format is known to have changed.
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

    private readonly record struct BridgeState(
        int MissingRows,
        int StaleRows);
}
