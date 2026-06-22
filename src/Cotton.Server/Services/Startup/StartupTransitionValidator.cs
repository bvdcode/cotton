// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Cotton.Server.Services.Startup
{
    internal class StartupTransitionValidator(
        CottonDbContext _dbContext,
        ILogger<StartupTransitionValidator> _logger) : IStartupCheck
    {
        public async Task<StartupBlocker?> ValidateAsync(CancellationToken cancellationToken)
        {
            string? currentVersion = AppVersionHelpers.GetAppVersion();
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                _logger.LogInformation("APP_VERSION is not configured; startup transition rules are not applied.");
                return null;
            }

            StartupTransitionRule[] activeRules = StartupTransitionRules.All
                .Where(rule => rule.AppliesTo(currentVersion))
                .ToArray();
            if (activeRules.Length == 0)
            {
                return null;
            }

            if (!await HasExistingTablesAsync(cancellationToken))
            {
                return null;
            }

            List<AppVersion> versionHistory;
            try
            {
                versionHistory = await _dbContext.AppVersions
                    .AsNoTracking()
                    .OrderByDescending(version => version.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsAppVersionHistoryUnavailable(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Startup transition validation could not read the app version history.");
                return activeRules[0].CreateBlocker(currentVersion, lastRecordedVersion: null);
            }

            string? lastRecordedVersion = versionHistory.FirstOrDefault()?.Version;
            DateTime utcNow = DateTime.UtcNow;
            foreach (StartupTransitionRule rule in activeRules)
            {
                if (versionHistory.Any(version => rule.IsSatisfiedBy(version.Version, version.CreatedAt, utcNow)))
                {
                    continue;
                }

                _logger.LogCritical(
                    "Startup blocked for Cotton version {CurrentVersion}. Required previous version range {RequiredVersionRange} was not found in app_versions; last recorded version was {LastRecordedVersion}.",
                    currentVersion,
                    rule.RequiredVersionRange,
                    lastRecordedVersion ?? "<none>");
                return rule.CreateBlocker(currentVersion, lastRecordedVersion);
            }

            return null;
        }

        private async Task<bool> HasExistingTablesAsync(CancellationToken cancellationToken)
        {
            IRelationalDatabaseCreator creator = _dbContext.GetService<IRelationalDatabaseCreator>();
            if (!await creator.ExistsAsync(cancellationToken))
            {
                return false;
            }

            return await creator.HasTablesAsync(cancellationToken);
        }

        private static bool IsAppVersionHistoryUnavailable(PostgresException exception)
        {
            return exception.SqlState is PostgresErrorCodes.UndefinedTable
                or PostgresErrorCodes.UndefinedColumn;
        }
    }
}
