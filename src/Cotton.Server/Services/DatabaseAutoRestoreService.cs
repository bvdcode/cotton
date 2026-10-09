// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models.DatabaseBackup;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Cotton.Server.Services
{
    public class DatabaseAutoRestoreService(
        IConfiguration configuration,
        CottonDbContext dbContext,
        DatabaseBackupRestorePreparation preparation,
        IPostgresDumpService postgresDump,
        IDatabaseBackupManifestService backupManifestService,
        INotificationsProvider notificationsProvider,
        ILogger<DatabaseAutoRestoreService> logger)
    {
        private const string RestoreEnvKey = "COTTON_RESTORE_DATABASE_IF_EMPTY";

        public async Task TryRestoreIfEmptyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRestoreEnabled())
            {
                return;
            }

            if (!await IsDatabaseEmptyAsync(cancellationToken))
            {
                logger.LogInformation("Automatic database restore skipped: database is not empty.");
                return;
            }

            BackupManifestPointer? pointer = await backupManifestService.ReadPointerAsync(cancellationToken);
            if (pointer is null)
            {
                logger.LogInformation("Automatic database restore skipped: latest backup manifest was not found.");
                return;
            }

            string dumpPath = BuildDumpFilePath(Guid.NewGuid().ToString("N"));
            try
            {
                PreparedDatabaseBackup prepared = await preparation.PrepareAsync(pointer, dumpPath, cancellationToken);
                ResolvedBackupManifest backup = prepared.Backup;
                logger.LogInformation("Restoring verified database backup {BackupId} created at {CreatedAtUtc}.",
                    backup.Manifest.BackupId, backup.Manifest.CreatedAtUtc);
                await EnsurePostgresExtensionsForRestoreAsync(cancellationToken);
                await ReloadPostgresTypesAsync(cancellationToken);
                await postgresDump.RestoreFromFileAsync(dumpPath, cancellationToken);
                await ReloadPostgresTypesAsync(cancellationToken);
                await dbContext.Database.MigrateAsync(cancellationToken);
                await ReloadPostgresTypesAsync(cancellationToken);
                await NotifyAdminsAboutRestoreAsync(prepared, cancellationToken);
                logger.LogInformation(
                    "Automatic database restore finished successfully. BackupId={BackupId}",
                    backup.Manifest.BackupId);
            }
            finally
            {
                TryDeleteDumpFile(dumpPath);
            }
        }

        private bool IsRestoreEnabled()
        {
            return bool.TryParse(configuration[RestoreEnvKey], out bool enabled) && enabled;
        }

        private async Task<bool> IsDatabaseEmptyAsync(CancellationToken cancellationToken)
        {
            IEnumerable<string> appliedMigrations = await dbContext.Database
                .GetAppliedMigrationsAsync(cancellationToken);
            bool hasAppliedMigrations = appliedMigrations.Any();
            if (!hasAppliedMigrations)
            {
                logger.LogInformation("Database considered empty: no applied migrations in __EFMigrationsHistory.");
                return true;
            }

            bool hasUsers = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(cancellationToken);
            bool hasServerSettings = await dbContext.ServerSettings
                .AsNoTracking()
                .AnyAsync(cancellationToken);
            if (!hasUsers && !hasServerSettings)
            {
                logger.LogInformation("Database considered empty: no users and no server settings rows.");
                return true;
            }

            return false;
        }

        private async Task EnsurePostgresExtensionsForRestoreAsync(CancellationToken cancellationToken)
        {
            // pg_restore replays dump schema before EF migrations can run, so extension-backed types
            // used by the dump must exist up front.
            await dbContext.Database.EnsurePostgresExtensionAsync("citext", cancellationToken);
            await dbContext.Database.EnsurePostgresExtensionAsync("hstore", cancellationToken);
        }

        private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
        {
            if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
            {
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
            }
        }


        private static string BuildDumpFilePath(string backupId)
        {
            string directory = Path.Combine(Path.GetTempPath(), "cotton", "db-restore");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"restore-{backupId}.dump");
        }

        private void TryDeleteDumpFile(string dumpPath)
        {
            try
            {
                if (File.Exists(dumpPath))
                {
                    File.Delete(dumpPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Failed to delete temporary database restore dump file at {DumpPath}.", dumpPath);
            }
        }

        private async Task ReloadPostgresTypesAsync(CancellationToken cancellationToken)
        {
            if (dbContext.Database.GetDbConnection() is not NpgsqlConnection npgsqlConnection)
            {
                return;
            }

            bool shouldCloseConnection = dbContext.Database.GetDbConnection().State != ConnectionState.Open;
            await EnsureConnectionOpenAsync(cancellationToken);
            try
            {
                await npgsqlConnection.ReloadTypesAsync(cancellationToken);
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }

        private async Task NotifyAdminsAboutRestoreAsync(PreparedDatabaseBackup prepared, CancellationToken cancellationToken)
        {
            ResolvedBackupManifest backup = prepared.Backup;
            List<Guid> adminIds = await dbContext.Users
                .AsNoTracking()
                .Where(x => x.Role == UserRole.Admin)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (adminIds.Count == 0)
            {
                logger.LogWarning("Automatic database restore completed, but no admin users were found for notification.");
                return;
            }

            TimeZoneInfo serverTimeZone = await ResolveServerTimeZoneAsync(cancellationToken);
            DateTime restoreCompletedUtc = DateTime.UtcNow;
            DateTime createdAtLocal = TimeZoneInfo.ConvertTimeFromUtc(backup.Manifest.CreatedAtUtc, serverTimeZone);
            DateTime restoreCompletedLocal = TimeZoneInfo.ConvertTimeFromUtc(restoreCompletedUtc, serverTimeZone);

            string title = NotificationTemplates.DatabaseRestoreCompletedTitle;
            string content = NotificationTemplates.DatabaseRestoreCompletedContent(
                backup.Manifest.BackupId,
                backup.Manifest.SourceDatabase,
                backup.Manifest.SourceHost,
                backup.Manifest.SourcePort,
                serverTimeZone.Id,
                backup.Manifest.CreatedAtUtc,
                createdAtLocal,
                restoreCompletedUtc,
                restoreCompletedLocal);
            if (prepared.SkippedBackupIds.Count > 0)
            {
                content += NotificationTemplates.DatabaseRestoreFallbackContent(string.Join(", ", prepared.SkippedBackupIds));
            }

            Dictionary<string, string> metadata = new()
            {
                ["backupId"] = backup.Manifest.BackupId,
                ["createdAtUtc"] = backup.Manifest.CreatedAtUtc.ToString("O"),
                ["createdAtLocal"] = createdAtLocal.ToString("O"),
                ["restoreCompletedUtc"] = restoreCompletedUtc.ToString("O"),
                ["restoreCompletedLocal"] = restoreCompletedLocal.ToString("O"),
                ["backupCreatedUtc"] = backup.Manifest.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["backupCreatedLocal"] = createdAtLocal.ToString("yyyy-MM-dd HH:mm:ss"),
                ["restoreCompletedUtcDisplay"] = restoreCompletedUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["restoreCompletedLocalDisplay"] = restoreCompletedLocal.ToString("yyyy-MM-dd HH:mm:ss"),
                ["sourceDatabase"] = backup.Manifest.SourceDatabase,
                ["sourceHost"] = backup.Manifest.SourceHost,
                ["sourcePort"] = backup.Manifest.SourcePort,
                ["serverTimezone"] = serverTimeZone.Id,
                ["manifestStorageKey"] = backup.ManifestStorageKey,
                ["skippedBackupIds"] = string.Join(", ", prepared.SkippedBackupIds)
            };
            Dictionary<string, string> templateMetadata = NotificationTemplateMetadata.Create(
                NotificationTemplateKeys.DatabaseRestoreCompletedTitle,
                prepared.SkippedBackupIds.Count > 0
                    ? NotificationTemplateKeys.DatabaseRestoreCompletedWithFallbackContent
                    : NotificationTemplateKeys.DatabaseRestoreCompletedContent,
                metadata);

            foreach (Guid adminId in adminIds)
            {
                await notificationsProvider.SendNotificationAsync(
                    userId: adminId,
                    title: title,
                    content: content,
                    priority: NotificationPriority.High,
                    metadata: templateMetadata);
            }
        }

        private async Task<TimeZoneInfo> ResolveServerTimeZoneAsync(CancellationToken cancellationToken)
        {
            string? timezone = await dbContext.ServerSettings
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Timezone)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(timezone)
                && TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out TimeZoneInfo? zone))
            {
                return zone;
            }

            return TimeZoneInfo.Utc;
        }
    }
}
