using System.Buffers;
using System.Data;
using System.Security.Cryptography;
using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Storage.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public class DatabaseAutoRestoreService(
        IConfiguration configuration,
        CottonDbContext dbContext,
        IStoragePipeline storage,
        IPostgresDumpService postgresDump,
        IDatabaseBackupManifestService backupManifestService,
        INotificationsProvider notificationsProvider,
        ILogger<DatabaseAutoRestoreService> logger) : IDatabaseAutoRestoreService
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

            ResolvedBackupManifest? backup = await backupManifestService.TryGetLatestManifestAsync(cancellationToken);
            if (backup is null)
            {
                logger.LogInformation("Automatic database restore skipped: latest backup manifest was not found.");
                return;
            }

            logger.LogInformation(
                "Automatic database restore requested. Found backup {BackupId} created at {CreatedAtUtc}. Starting restore.",
                backup.Manifest.BackupId,
                backup.Manifest.CreatedAtUtc);

            string dumpPath = BuildDumpFilePath(backup.Manifest.BackupId);
            try
            {
                await RebuildDumpFileAsync(backup.Manifest, dumpPath, cancellationToken);
                await postgresDump.RestoreFromFileAsync(dumpPath, cancellationToken);
                await EnsurePostgresExtensionsAsync(cancellationToken);
                await NotifyAdminsAboutRestoreAsync(backup, cancellationToken);
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
            bool hasAppliedMigrations = await TableHasRowsAsync("__EFMigrationsHistory", cancellationToken);
            if (!hasAppliedMigrations)
            {
                logger.LogInformation("Database considered empty: no applied migrations in __EFMigrationsHistory.");
                return true;
            }

            bool hasUsers = await TableHasRowsAsync("users", cancellationToken);
            bool hasServerSettings = await TableHasRowsAsync("server_settings", cancellationToken);
            if (!hasUsers && !hasServerSettings)
            {
                logger.LogInformation("Database considered empty: no users and no server settings rows.");
                return true;
            }

            return false;
        }

        private async Task<bool> TableHasRowsAsync(string tableName, CancellationToken cancellationToken)
        {
            if (!await TableExistsAsync(tableName, cancellationToken))
            {
                return false;
            }

            string quotedTableName = QuoteIdentifier(tableName);
            string sql = $"SELECT EXISTS (SELECT 1 FROM public.{quotedTableName} LIMIT 1);";
            return await ExecuteScalarBooleanAsync(sql, cancellationToken);
        }

        private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = @tableName
                );
                """;

            await EnsureConnectionOpenAsync(cancellationToken);
            using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool exists && exists;
        }

        private async Task<bool> ExecuteScalarBooleanAsync(string sql, CancellationToken cancellationToken)
        {
            await EnsureConnectionOpenAsync(cancellationToken);
            using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool value && value;
        }

        private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
        {
            if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
            {
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
            }
        }

        private static string QuoteIdentifier(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private async Task RebuildDumpFileAsync(BackupManifest manifest, string outputPath, CancellationToken cancellationToken)
        {
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            using var hasher = IncrementalHash.CreateHash(Hasher.SupportedHashAlgorithmName);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
            long totalBytes = 0;

            try
            {
                foreach (BackupChunkInfo chunk in manifest.Chunks.OrderBy(x => x.Order))
                {
                    await using Stream chunkStream = await storage.ReadAsync(chunk.StorageKey);
                    int bytesRead;
                    while ((bytesRead = await chunkStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        hasher.AppendData(buffer, 0, bytesRead);
                        await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalBytes += bytesRead;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            string contentHash = Hasher.ToHexStringHash(hasher.GetHashAndReset());
            if (!string.Equals(contentHash, manifest.DumpContentHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Restored dump hash does not match backup manifest hash.");
            }

            if (totalBytes != manifest.DumpSizeBytes)
            {
                throw new InvalidOperationException("Restored dump size does not match backup manifest size.");
            }
        }

        private static string BuildDumpFilePath(string backupId)
        {
            string directory = Path.Combine(Path.GetTempPath(), "cotton", "db-restore");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"restore-{backupId}.dump");
        }

        private static void TryDeleteDumpFile(string dumpPath)
        {
            try
            {
                if (File.Exists(dumpPath))
                {
                    File.Delete(dumpPath);
                }
            }
            catch
            {
            }
        }

        private async Task EnsurePostgresExtensionsAsync(CancellationToken cancellationToken)
        {
            await dbContext.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS citext;", cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS hstore;", cancellationToken);
        }

        private async Task NotifyAdminsAboutRestoreAsync(ResolvedBackupManifest backup, CancellationToken cancellationToken)
        {
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

            string title = NotificationTemplates.DatabaseRestoreCompletedTitle;
            string content = NotificationTemplates.DatabaseRestoreCompletedContent(
                backup.Manifest.BackupId,
                backup.Manifest.CreatedAtUtc);

            Dictionary<string, string> metadata = new()
            {
                ["backupId"] = backup.Manifest.BackupId,
                ["createdAtUtc"] = backup.Manifest.CreatedAtUtc.ToString("O"),
                ["manifestStorageKey"] = backup.ManifestStorageKey
            };

            foreach (Guid adminId in adminIds)
            {
                await notificationsProvider.SendNotificationAsync(
                    userId: adminId,
                    title: title,
                    content: content,
                    priority: NotificationPriority.High,
                    metadata: metadata);
            }
        }
    }
}
