// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Providers;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates file version retention.
    /// </summary>
    public sealed class FileVersionRetentionService(
        CottonDbContext _dbContext,
        SettingsProvider _settingsProvider,
        FileVersionStorageService _storage)
    {
        private const int LimitedHistoricalVersionCount = 2;
        private const int OptimalHistoricalVersionCount = 10;

        /// <summary>
        /// Prunes historical versions of the given lineage down to the configured
        /// retention limit and returns the total number of bytes removed.
        /// </summary>
        public async Task<long> ApplyAsync(
            Guid userId,
            Guid lineageId,
            IReadOnlySet<Guid>? protectedVersionIds = null,
            CancellationToken ct = default)
        {
            int? maxHistoricalVersions = ResolveMaxHistoricalVersions();
            if (!maxHistoricalVersions.HasValue)
            {
                return 0;
            }

            List<NodeFile> historicalVersions = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .Where(x => x.OwnerId == userId
                    && x.OriginalNodeFileId == lineageId
                    && x.Id != lineageId)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            if (historicalVersions.Count <= maxHistoricalVersions.Value)
            {
                return 0;
            }

            int pruneCount = historicalVersions.Count - maxHistoricalVersions.Value;
            NodeFile[] versionsToPrune =
            [
                .. historicalVersions
                    .Skip(1)
                    .Where(x => protectedVersionIds is null || !protectedVersionIds.Contains(x.Id))
                    .Take(pruneCount),
            ];
            return await _storage.DeleteHistoricalVersionsAsync(userId, versionsToPrune, ct);
        }

        private int? ResolveMaxHistoricalVersions()
        {
            return _settingsProvider.GetServerSettings().StorageSpaceMode switch
            {
                StorageSpaceMode.Limited => LimitedHistoricalVersionCount,
                StorageSpaceMode.Optimal => OptimalHistoricalVersionCount,
                StorageSpaceMode.Unlimited => null,
                _ => OptimalHistoricalVersionCount,
            };
        }
    }
}
