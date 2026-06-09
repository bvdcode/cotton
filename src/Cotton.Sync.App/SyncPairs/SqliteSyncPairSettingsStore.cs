// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.App.SyncPairs
{

    /// <summary>
    /// Persists sync-pair settings in a SQLite database through Entity Framework Core.
    /// </summary>
    public sealed class SqliteSyncPairSettingsStore : ISyncPairSettingsStore
    {
        private readonly SqliteSyncAppDbContextFactory _contextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteSyncPairSettingsStore" /> class.
        /// </summary>
        public SqliteSyncPairSettingsStore(string databasePath)
        {
            _contextFactory = new SqliteSyncAppDbContextFactory(databasePath);
        }

        /// <inheritdoc />
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _contextFactory.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SyncPairSettings>> ListAsync(CancellationToken cancellationToken = default)
        {
            await using SyncAppDbContext context = _contextFactory.Create();
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
            await using SyncAppDbContext context = _contextFactory.Create();
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
            await using SyncAppDbContext context = _contextFactory.Create();
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
            await using SyncAppDbContext context = _contextFactory.Create();
            SyncPairSettingsEntity? entity = await context.SyncPairSettings
                .SingleOrDefaultAsync(item => item.Id == syncPairId, cancellationToken)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return;
            }

            context.SyncPairSettings.Remove(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void UpdateEntity(SyncPairSettingsEntity entity, SyncPairSettings syncPair)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(syncPair.Mode, SyncPairMode.Unknown);
            DateTime now = DateTime.UtcNow;
            entity.DisplayName = syncPair.DisplayName.Trim();
            entity.LocalRootPath = syncPair.LocalRootPath.Trim();
            entity.RemoteRootNodeId = syncPair.RemoteRootNodeId;
            entity.RemoteDisplayPath = syncPair.RemoteDisplayPath.Trim();
            entity.IsEnabled = syncPair.IsEnabled;
            entity.Mode = syncPair.Mode;
            entity.CreatedAtUtc = UtcDateTime.Normalize(syncPair.CreatedAtUtc == default ? now : syncPair.CreatedAtUtc);
            entity.UpdatedAtUtc = UtcDateTime.Normalize(syncPair.UpdatedAtUtc == default ? now : syncPair.UpdatedAtUtc);
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
                Mode = NormalizeStoredMode(entity.Mode),
                CreatedAtUtc = UtcDateTime.Normalize(entity.CreatedAtUtc),
                UpdatedAtUtc = UtcDateTime.Normalize(entity.UpdatedAtUtc),
            };
        }

        private static SyncPairMode NormalizeStoredMode(SyncPairMode mode)
        {
            return mode == SyncPairMode.Unknown ? SyncPairMode.FullMirror : mode;
        }
    }
}
