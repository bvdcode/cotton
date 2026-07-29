// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Helpers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Cotton.Server.Services.Startup
{
    [Obsolete("OBSOLETE TRANSITION: this state guard exists only for the 0.5 CTN2/database-integrity cutover. Remove it after the supported upgrade window closes.")]
    internal class Ctn2IntegrityTransitionStartupCheck(
        CottonDbContext _dbContext,
        DatabaseIntegrityDiagnosticsService _integrityDiagnostics,
        IStoragePipeline _storage,
        ILogger<Ctn2IntegrityTransitionStartupCheck> _logger) : IStartupCheck
    {
        private const string RequiredTransitionVersion = "0.4.35";

        public async Task<StartupBlocker?> ValidateAsync(CancellationToken cancellationToken)
        {
            if (!await HasExistingTablesAsync(cancellationToken))
            {
                return null;
            }

            bool hasExistingData;
            try
            {
                hasExistingData = await HasExistingDataAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsTransitionSchemaUnavailable(ex))
            {
                _logger.LogCritical(
                    ex,
                    "The CTN2/database-integrity transition state could not be read from the existing database.");
                return CreateBlocker(
                    "The existing database does not contain the completed 0.4.35 transition schema.");
            }

            if (!hasExistingData)
            {
                return null;
            }

            DatabaseIntegrityDiagnosticsDto integrity;
            try
            {
                integrity = await _integrityDiagnostics.GetSnapshotAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsTransitionSchemaUnavailable(ex))
            {
                _logger.LogCritical(
                    ex,
                    "The CTN2/database-integrity transition metadata is missing from the existing database.");
                return CreateBlocker(
                    "The existing database is missing database-integrity transition metadata.");
            }

            if (integrity.UnsignedProtectedRows > 0)
            {
                _logger.LogCritical(
                    "Startup blocked because {UnsignedProtectedRows} protected database rows have not completed the integrity transition.",
                    integrity.UnsignedProtectedRows);
                return CreateBlocker(
                    $"{integrity.UnsignedProtectedRows} protected database rows are unsigned or use an obsolete integrity schema.");
            }

            if (!await _storage.ExistsAsync(Ctn2IntegrityTransitionState.CompletionStorageMarkerKey))
            {
                _logger.LogCritical(
                    "Startup blocked because the durable CTN2 rewrite completion marker {StorageKey} is missing.",
                    Ctn2IntegrityTransitionState.CompletionStorageMarkerKey);
                return CreateBlocker("The durable CTN2 storage rewrite completion marker is missing.");
            }

            return null;
        }

        private async Task<bool> HasExistingDataAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.Users.AsNoTracking().AnyAsync(cancellationToken)
                || await _dbContext.ServerSettings.AsNoTracking().AnyAsync(cancellationToken)
                || await _dbContext.Nodes.AsNoTracking().AnyAsync(cancellationToken)
                || await _dbContext.FileManifests.AsNoTracking().AnyAsync(cancellationToken)
                || await _dbContext.Chunks.AsNoTracking().AnyAsync(cancellationToken);
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

        private static bool IsTransitionSchemaUnavailable(PostgresException exception)
        {
            return exception.SqlState is PostgresErrorCodes.UndefinedTable
                or PostgresErrorCodes.UndefinedColumn;
        }

        private static StartupBlocker CreateBlocker(string reason)
        {
            return new StartupBlocker
            {
                Kind = "ctn2-integrity-transition-required",
                Title = "Cotton 0.4.35 must complete the storage and database-integrity transition.",
                Message = $"{reason} Start Cotton 0.4.35 with the same database, storage, and master key, wait for the transition to complete, then start this version again.",
                CurrentVersion = AppVersionHelpers.GetAppVersion(),
                RequiredVersion = RequiredTransitionVersion,
                RequiredVersionRange = RequiredTransitionVersion,
            };
        }
    }
}
