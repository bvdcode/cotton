// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.State;

/// <summary>
/// Persists sync baselines in a SQLite database through Entity Framework Core.
/// </summary>
public sealed class SqliteSyncStateStore : ISyncStateStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MigrationGates = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _databasePath;
    private bool _initialized;

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
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(string syncPairId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using SyncStateDbContext context = CreateContext();
        IAsyncEnumerable<SyncStateEntity> entities = context.SyncEntries
            .AsNoTracking()
            .Where(entry => entry.SyncPairId == syncPairId)
            .OrderBy(entry => entry.RelativePathKey)
            .AsAsyncEnumerable();
        var entries = new List<SyncStateEntry>();
        await foreach (SyncStateEntity entity in entities.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(ToModel(entity));
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<SyncChangeCursor> GetChangeCursorAsync(string syncPairId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using SyncStateDbContext context = CreateContext();
        SyncChangeCursorEntity? entity = await context.SyncChangeCursors
            .AsNoTracking()
            .SingleOrDefaultAsync(
                cursor => cursor.SyncPairId == syncPairId,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? CreateDefaultCursor(syncPairId) : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<SyncStateEntry?> GetAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        string key = SyncPath.ToKey(relativePath);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
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
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
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
    public async Task SaveChangeCursorAsync(SyncChangeCursor cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursor.SyncPairId);
        ValidateCursor(cursor);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using SyncStateDbContext context = CreateContext();
        SyncChangeCursorEntity? entity = await context.SyncChangeCursors
            .SingleOrDefaultAsync(
                existing => existing.SyncPairId == cursor.SyncPairId,
                cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new SyncChangeCursorEntity
            {
                SyncPairId = cursor.SyncPairId,
            };
            context.SyncChangeCursors.Add(entity);
        }

        UpdateEntity(entity, cursor);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        string key = SyncPath.ToKey(relativePath);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using SyncStateDbContext context = CreateContext();
        await context.SyncEntries
            .Where(entry => entry.SyncPairId == syncPairId && entry.RelativePathKey == key)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeletePairAsync(string syncPairId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using SyncStateDbContext context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.SyncEntries
            .Where(entry => entry.SyncPairId == syncPairId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await context.SyncChangeCursors
            .Where(cursor => cursor.SyncPairId == syncPairId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReplacePairAsync(
        string syncPairId,
        IReadOnlyCollection<SyncStateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        ArgumentNullException.ThrowIfNull(entries);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        EnsureDirectoryExists();
        string fullPath = Path.GetFullPath(_databasePath);
        if (_initialized && File.Exists(fullPath))
        {
            return;
        }

        SemaphoreSlim gate = MigrationGates.GetOrAdd(fullPath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized && File.Exists(fullPath))
            {
                return;
            }

            await using SyncStateDbContext context = CreateContext();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            gate.Release();
        }
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

    private static SyncChangeCursor CreateDefaultCursor(string syncPairId)
    {
        return new SyncChangeCursor
        {
            SyncPairId = syncPairId,
            LastCursor = 0,
            UpdatedAtUtc = DateTime.UtcNow,
        };
    }

    private static void UpdateEntity(SyncChangeCursorEntity entity, SyncChangeCursor cursor)
    {
        entity.SyncPairId = cursor.SyncPairId;
        entity.LastCursor = cursor.LastCursor;
        entity.CursorExpired = cursor.CursorExpired;
        entity.EarliestAvailableCursor = cursor.EarliestAvailableCursor;
        entity.UpdatedAtUtc = ToUtc(cursor.UpdatedAtUtc) ?? DateTime.UtcNow;
    }

    private static SyncChangeCursor ToModel(SyncChangeCursorEntity entity)
    {
        return new SyncChangeCursor
        {
            SyncPairId = entity.SyncPairId,
            LastCursor = entity.LastCursor,
            CursorExpired = entity.CursorExpired,
            EarliestAvailableCursor = entity.EarliestAvailableCursor,
            UpdatedAtUtc = ToUtc(entity.UpdatedAtUtc) ?? DateTime.UtcNow,
        };
    }

    private static void ValidateCursor(SyncChangeCursor cursor)
    {
        if (cursor.LastCursor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), cursor.LastCursor, "Change cursor cannot be negative.");
        }

        if (cursor.EarliestAvailableCursor < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cursor),
                cursor.EarliestAvailableCursor,
                "Earliest available cursor cannot be negative.");
        }
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
