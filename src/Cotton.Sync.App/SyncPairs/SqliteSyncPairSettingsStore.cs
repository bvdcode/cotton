// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.App.SyncPairs;

/// <summary>
/// Persists sync-pair settings in a SQLite database through Entity Framework Core.
/// </summary>
public sealed class SqliteSyncPairSettingsStore : ISyncPairSettingsStore
{
    private readonly string _databasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteSyncPairSettingsStore" /> class.
    /// </summary>
    public SqliteSyncPairSettingsStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists();
        await using SyncPairSettingsDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncPairSettings>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using SyncPairSettingsDbContext context = CreateContext();
        List<SyncPairSettingsEntity> entities = await context.SyncPairSettings
            .AsNoTracking()
            .OrderBy(static entity => entity.DisplayName)
            .ThenBy(static entity => entity.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<SyncPairSettings?> GetAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        await using SyncPairSettingsDbContext context = CreateContext();
        SyncPairSettingsEntity? entity = await context.SyncPairSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == syncPairId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncPair);
        await using SyncPairSettingsDbContext context = CreateContext();
        SyncPairSettingsEntity? entity = await context.SyncPairSettings
            .SingleOrDefaultAsync(item => item.Id == syncPair.Id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new SyncPairSettingsEntity { Id = syncPair.Id };
            context.SyncPairSettings.Add(entity);
        }

        UpdateEntity(entity, syncPair);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        await using SyncPairSettingsDbContext context = CreateContext();
        await context.SyncPairSettings
            .Where(item => item.Id == syncPairId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static void UpdateEntity(SyncPairSettingsEntity entity, SyncPairSettings syncPair)
    {
        DateTime now = DateTime.UtcNow;
        entity.DisplayName = syncPair.DisplayName.Trim();
        entity.LocalRootPath = syncPair.LocalRootPath.Trim();
        entity.RemoteRootNodeId = syncPair.RemoteRootNodeId;
        entity.RemoteDisplayPath = syncPair.RemoteDisplayPath.Trim();
        entity.IsEnabled = syncPair.IsEnabled;
        entity.Mode = syncPair.Mode;
        entity.CreatedAtUtc = ToUtc(syncPair.CreatedAtUtc == default ? now : syncPair.CreatedAtUtc);
        entity.UpdatedAtUtc = ToUtc(syncPair.UpdatedAtUtc == default ? now : syncPair.UpdatedAtUtc);
    }

    private static SyncPairSettings ToModel(SyncPairSettingsEntity entity)
    {
        return new SyncPairSettings
        {
            Id = entity.Id,
            DisplayName = entity.DisplayName,
            LocalRootPath = entity.LocalRootPath,
            RemoteRootNodeId = entity.RemoteRootNodeId,
            RemoteDisplayPath = entity.RemoteDisplayPath,
            IsEnabled = entity.IsEnabled,
            Mode = entity.Mode,
            CreatedAtUtc = ToUtc(entity.CreatedAtUtc),
            UpdatedAtUtc = ToUtc(entity.UpdatedAtUtc),
        };
    }

    private SyncPairSettingsDbContext CreateContext()
    {
        var connectionString = new DbConnectionStringBuilder
        {
            ["Data Source"] = _databasePath,
            ["Pooling"] = false,
        }.ToString();
        DbContextOptions<SyncPairSettingsDbContext> options = new DbContextOptionsBuilder<SyncPairSettingsDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new SyncPairSettingsDbContext(options);
    }

    private void EnsureDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime(),
        };
    }
}
