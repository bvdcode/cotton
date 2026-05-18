using Cotton.Storage.Backends;
using EasyExtensions.Crypto;
using EasyExtensions.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    public interface IMasterKeyCompatibilityProbe
    {
        Task<MasterKeyCompatibilityResult> ValidateAsync(
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken = default);
    }

    public enum MasterKeyCompatibilityMode
    {
        AllowMissingEvidence,
        RequireEvidenceForExistingData
    }

    public sealed record MasterKeyCompatibilityResult(
        bool Success,
        bool ExistingDataFound,
        bool EvidenceFound,
        string? Error)
    {
        public static MasterKeyCompatibilityResult Compatible(bool existingDataFound, bool evidenceFound) =>
            new(true, existingDataFound, evidenceFound, null);

        public static MasterKeyCompatibilityResult Fail(
            string error,
            bool existingDataFound = true,
            bool evidenceFound = false) =>
            new(false, existingDataFound, evidenceFound, error);
    }

    public sealed class MasterKeyCompatibilityProbe : IMasterKeyCompatibilityProbe
    {
        private static readonly string[] CottonDataTables =
        [
            "users",
            "nodes",
            "file_manifests",
            "chunks",
            "server_settings"
        ];

        private static readonly EncryptedColumnProbe[] EncryptedColumnProbes =
        [
            new("users", "totp_secret_encrypted", EncryptedColumnKind.Bytea),
            new("users", "avatar_hash_encrypted", EncryptedColumnKind.Bytea),
            new("file_manifests", "small_file_preview_hash_encrypted", EncryptedColumnKind.Bytea),
            new("server_settings", "cloud_services_token_encrypted", EncryptedColumnKind.Base64String),
            new("server_settings", "oidc_client_secret_encrypted", EncryptedColumnKind.Base64String),
            new("server_settings", "s3_secret_access_key_encrypted", EncryptedColumnKind.Base64String),
            new("server_settings", "smtp_password_encrypted", EncryptedColumnKind.Base64String)
        ];

        private readonly ILogger<MasterKeyCompatibilityProbe> _logger;
        private readonly string? _connectionString;
        private readonly FileSystemStorageBackend _fileSystemStorage;

        public MasterKeyCompatibilityProbe(
            ILogger<MasterKeyCompatibilityProbe> logger,
            string? connectionString = null,
            string? storageBasePath = null)
        {
            _logger = logger;
            _connectionString = connectionString;
            _fileSystemStorage = new FileSystemStorageBackend(
                NullLogger<FileSystemStorageBackend>.Instance,
                storageBasePath);
        }

        public async Task<MasterKeyCompatibilityResult> ValidateAsync(
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString ?? BuildConnectionStringFromEnvironment());
                await connection.OpenAsync(cancellationToken);

                bool existingDataFound = await HasExistingCottonDataAsync(connection, cancellationToken);
                if (!existingDataFound)
                {
                    return MasterKeyCompatibilityResult.Compatible(existingDataFound: false, evidenceFound: false);
                }

                using AesGcmStreamCipher cipher = MasterKeySentinelStore.CreateCipher(encryptionSettings);
                if (await TryValidateEncryptedDatabaseProbeAsync(connection, cipher, cancellationToken))
                {
                    return MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: true);
                }

                if (await TryValidateFileSystemChunkProbeAsync(connection, cipher, cancellationToken))
                {
                    return MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: true);
                }

                if (mode == MasterKeyCompatibilityMode.RequireEvidenceForExistingData)
                {
                    return MasterKeyCompatibilityResult.Fail(
                        "Existing Cotton data was found, but no encrypted data could be used to verify the submitted master key. Start Cotton once with COTTON_MASTER_KEY set to the original master key so it can seed the master-key sentinel safely.",
                        existingDataFound: true,
                        evidenceFound: false);
                }

                _logger.LogInformation(
                    "Existing Cotton data found, but no encrypted compatibility probe was available. Trusting the existing master-key sentinel.");
                return MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: false);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
            {
                return MasterKeyCompatibilityResult.Compatible(existingDataFound: false, evidenceFound: false);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning(ex, "Submitted master key failed compatibility validation against existing encrypted Cotton data.");
                return MasterKeyCompatibilityResult.Fail("Master key does not match existing encrypted Cotton data.", evidenceFound: true);
            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(ex, "Submitted master key failed compatibility validation against existing encrypted Cotton data.");
                return MasterKeyCompatibilityResult.Fail("Master key does not match existing encrypted Cotton data.", evidenceFound: true);
            }
            catch (Exception ex) when (ex is NpgsqlException
                or TimeoutException
                or FormatException
                or InvalidOperationException
                or ArgumentException)
            {
                _logger.LogWarning(ex, "Could not verify master key compatibility with existing Cotton data.");
                return MasterKeyCompatibilityResult.Fail(
                    $"Could not verify master key compatibility with the existing database: {ex.Message}",
                    existingDataFound: false,
                    evidenceFound: false);
            }
        }

        private static string BuildConnectionStringFromEnvironment()
        {
            string postgresHost = Environment.GetEnvironmentVariable("COTTON_PG_HOST") ?? "localhost";
            string postgresPortStr = Environment.GetEnvironmentVariable("COTTON_PG_PORT") ?? "5432";
            string postgresDb = Environment.GetEnvironmentVariable("COTTON_PG_DATABASE") ?? "cotton_dev";
            string postgresUser = Environment.GetEnvironmentVariable("COTTON_PG_USERNAME") ?? "postgres";
            string postgresPass = Environment.GetEnvironmentVariable("COTTON_PG_PASSWORD") ?? "postgres";

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = postgresHost,
                Port = int.Parse(postgresPortStr),
                Database = postgresDb,
                Username = postgresUser,
                Password = postgresPass
            };
            return builder.ConnectionString;
        }

        private static async Task<bool> HasExistingCottonDataAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
        {
            foreach (string tableName in CottonDataTables)
            {
                if (await TableExistsAsync(connection, tableName, cancellationToken)
                    && await RowExistsAsync(connection, tableName, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<bool> TryValidateEncryptedDatabaseProbeAsync(
            NpgsqlConnection connection,
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            foreach (EncryptedColumnProbe probe in EncryptedColumnProbes)
            {
                if (!await TableExistsAsync(connection, probe.TableName, cancellationToken)
                    || !await ColumnExistsAsync(connection, probe.TableName, probe.ColumnName, cancellationToken))
                {
                    continue;
                }

                byte[]? encrypted = await ReadEncryptedColumnProbeAsync(connection, probe, cancellationToken);
                if (encrypted is null)
                {
                    continue;
                }

                _ = cipher.Decrypt(encrypted);
                return true;
            }

            return false;
        }

        private async Task<bool> TryValidateFileSystemChunkProbeAsync(
            NpgsqlConnection connection,
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            if (!await TableExistsAsync(connection, "chunks", cancellationToken)
                || !await ColumnExistsAsync(connection, "chunks", "hash", cancellationToken))
            {
                return false;
            }

            IReadOnlyList<string> storageKeys = await ReadCandidateChunkStorageKeysAsync(connection, cancellationToken);
            foreach (string storageKey in storageKeys)
            {
                if (!await _fileSystemStorage.ExistsAsync(storageKey))
                {
                    continue;
                }

                await using Stream encrypted = await _fileSystemStorage.ReadAsync(storageKey);
                await using Stream decrypted = await cipher.DecryptAsync(encrypted);
                await decrypted.CopyToAsync(Stream.Null, cancellationToken);
                return true;
            }

            return false;
        }

        private static async Task<byte[]?> ReadEncryptedColumnProbeAsync(
            NpgsqlConnection connection,
            EncryptedColumnProbe probe,
            CancellationToken cancellationToken)
        {
            string nonEmptyTextFilter = probe.Kind == EncryptedColumnKind.Base64String
                ? $" and {probe.ColumnName} <> ''"
                : string.Empty;
            string sql =
                $"select {probe.ColumnName} from public.{probe.TableName} where {probe.ColumnName} is not null{nonEmptyTextFilter} limit 1";

            await using var command = new NpgsqlCommand(sql, connection);
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is null or DBNull)
            {
                return null;
            }

            return probe.Kind switch
            {
                EncryptedColumnKind.Bytea => (byte[])value,
                EncryptedColumnKind.Base64String => TryDecodeBase64((string)value),
                _ => null
            };
        }

        private static async Task<IReadOnlyList<string>> ReadCandidateChunkStorageKeysAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
        {
            bool hasStoredSize = await ColumnExistsAsync(connection, "chunks", "stored_size_bytes", cancellationToken);
            string orderBy = hasStoredSize
                ? " order by stored_size_bytes asc nulls last"
                : string.Empty;
            string sql = $"select hash from public.chunks where hash is not null{orderBy} limit 4";

            var storageKeys = new List<string>();
            await using var command = new NpgsqlCommand(sql, connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                byte[] hash = reader.GetFieldValue<byte[]>(0);
                storageKeys.Add(Hasher.ToHexStringHash(hash));
            }

            return storageKeys;
        }

        private static async Task<bool> TableExistsAsync(
            NpgsqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var command = new NpgsqlCommand("select to_regclass(@table_name) is not null", connection);
            command.Parameters.AddWithValue("table_name", $"public.{tableName}");
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true;
        }

        private static async Task<bool> ColumnExistsAsync(
            NpgsqlConnection connection,
            string tableName,
            string columnName,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select exists (
                    select 1
                    from information_schema.columns
                    where table_schema = 'public'
                        and table_name = @table_name
                        and column_name = @column_name
                )
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("table_name", tableName);
            command.Parameters.AddWithValue("column_name", columnName);
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true;
        }

        private static async Task<bool> RowExistsAsync(
            NpgsqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var command = new NpgsqlCommand(
                $"select exists (select 1 from public.{tableName} limit 1)",
                connection);
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true;
        }

        private static byte[]? TryDecodeBase64(string value)
        {
            try
            {
                return Convert.FromBase64String(value);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private sealed record EncryptedColumnProbe(
            string TableName,
            string ColumnName,
            EncryptedColumnKind Kind);

        private enum EncryptedColumnKind
        {
            Bytea,
            Base64String
        }
    }
}
