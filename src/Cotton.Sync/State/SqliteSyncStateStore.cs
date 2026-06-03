// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.State;

/// <summary>
/// Persists sync baselines in a SQLite database through Entity Framework Core.
/// </summary>
public sealed class SqliteSyncStateStore : ISyncStateStore
{
    private readonly string _databasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteSyncStateStore" /> class.
    /// </summary>
    public SqliteSyncStateStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists();
        await using SyncStateDbContext context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(string syncPairId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        await using SyncStateDbContext context = CreateContext();
        List<SyncStateEntity> entities = await context.SyncEntries
            .AsNoTracking()
            .Where(entry => entry.SyncPairId == syncPairId)
            .OrderBy(entry => entry.RelativePathKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<SyncStateEntry?> GetAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        string key = SyncPath.ToKey(relativePath);
        await using SyncStateDbContext context = CreateContext();
        SyncStateEntity? entity = await context.SyncEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.SyncPairId == syncPairId && entry.RelativePathKey == key,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.SyncPairId);
        entry.RelativePath = SyncPath.Normalize(entry.RelativePath);
        if (entry.SyncedAtUtc == default)
        {
            entry.SyncedAtUtc = DateTime.UtcNow;
        }

        string key = SyncPath.ToKey(entry.RelativePath);
        await using SyncStateDbContext context = CreateContext();
        SyncStateEntity? entity = await context.SyncEntries
            .SingleOrDefaultAsync(
                existing => existing.SyncPairId == entry.SyncPairId && existing.RelativePathKey == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new SyncStateEntity
            {
                SyncPairId = entry.SyncPairId,
                RelativePathKey = key,
            };
            context.SyncEntries.Add(entity);
        }

        UpdateEntity(entity, entry, key);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        string key = SyncPath.ToKey(relativePath);
        await using SyncStateDbContext context = CreateContext();
        await context.SyncEntries
            .Where(entry => entry.SyncPairId == syncPairId && entry.RelativePathKey == key)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReplacePairAsync(
        string syncPairId,
        IReadOnlyCollection<SyncStateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        ArgumentNullException.ThrowIfNull(entries);
        await using SyncStateDbContext context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.SyncEntries
            .Where(entry => entry.SyncPairId == syncPairId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (SyncStateEntry entry in entries)
        {
            entry.SyncPairId = syncPairId;
            entry.RelativePath = SyncPath.Normalize(entry.RelativePath);
            string key = SyncPath.ToKey(entry.RelativePath);
            var entity = new SyncStateEntity
            {
                SyncPairId = syncPairId,
                RelativePathKey = key,
            };
            UpdateEntity(entity, entry, key);
            context.SyncEntries.Add(entity);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void UpdateEntity(SyncStateEntity entity, SyncStateEntry entry, string key)
    {
        entity.SyncPairId = entry.SyncPairId;
        entity.RelativePathKey = key;
        entity.RelativePath = SyncPath.Normalize(entry.RelativePath);
        entity.Kind = entry.Kind;
        entity.LocalContentHash = NormalizeNullable(entry.LocalContentHash);
        entity.LocalLastWriteUtc = ToUtc(entry.LocalLastWriteUtc);
        entity.RemoteNodeId = entry.RemoteNodeId;
        entity.RemoteFileId = entry.RemoteFileId;
        entity.RemoteContentHash = NormalizeNullable(entry.RemoteContentHash);
        entity.RemoteETag = NormalizeNullable(entry.RemoteETag);
        entity.SyncedAtUtc = ToUtc(entry.SyncedAtUtc) ?? DateTime.UtcNow;
    }

    private static SyncStateEntry ToModel(SyncStateEntity entity)
    {
        return new SyncStateEntry
        {
            SyncPairId = entity.SyncPairId,
            RelativePath = entity.RelativePath,
            Kind = entity.Kind,
            LocalContentHash = entity.LocalContentHash,
            LocalLastWriteUtc = ToUtc(entity.LocalLastWriteUtc),
            RemoteNodeId = entity.RemoteNodeId,
            RemoteFileId = entity.RemoteFileId,
            RemoteContentHash = entity.RemoteContentHash,
            RemoteETag = entity.RemoteETag,
            SyncedAtUtc = ToUtc(entity.SyncedAtUtc) ?? DateTime.UtcNow,
        };
    }

    private SyncStateDbContext CreateContext()
    {
        var connectionString = new DbConnectionStringBuilder
        {
            ["Data Source"] = _databasePath,
            ["Pooling"] = false,
        }.ToString();
        DbContextOptions<SyncStateDbContext> options = new DbContextOptionsBuilder<SyncStateDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new SyncStateDbContext(options);
    }

    private void EnsureDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        return value?.Kind switch
        {
            null => null,
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => value.Value.ToUniversalTime(),
        };
    }
}
