// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Crypto;
using EasyExtensions.Extensions;
using Npgsql;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Defines the master key compatibility probe contract used by the server runtime.
    /// </summary>
    public interface IMasterKeyCompatibilityProbe
    {
        /// <summary>
        /// Validates that the supplied master key is compatible with any existing encrypted Cotton data.
        /// </summary>
        Task<MasterKeyCompatibilityResult> ValidateAsync(
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Lists the supported master key compatibility mode values.
    /// </summary>
    public enum MasterKeyCompatibilityMode
    {
        /// <summary>
        /// Represents the allow missing evidence option.
        /// </summary>
        AllowMissingEvidence,
        /// <summary>
        /// Represents the require evidence for existing data option.
        /// </summary>
        RequireEvidenceForExistingData
    }

    /// <summary>
    /// Represents the result of master key compatibility.
    /// </summary>
    public record MasterKeyCompatibilityResult(
        bool Success,
        bool ExistingDataFound,
        bool EvidenceFound,
        string? Error)
    {
        /// <summary>
        /// Creates a successful master-key compatibility result.
        /// </summary>
        public static MasterKeyCompatibilityResult Compatible(bool existingDataFound, bool evidenceFound) =>
            new(true, existingDataFound, evidenceFound, null);

        /// <summary>
        /// Creates a failed compatibility probe result.
        /// </summary>
        public static MasterKeyCompatibilityResult Fail(
            string error,
            bool existingDataFound = true,
            bool evidenceFound = false) =>
            new(false, existingDataFound, evidenceFound, error);
    }

    /// <summary>
    /// Represents master key compatibility probe.
    /// </summary>
    public class MasterKeyCompatibilityProbe : IMasterKeyCompatibilityProbe
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
        private readonly IStorageBackend _storage;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterKeyCompatibilityProbe"/> type.
        /// </summary>
        public MasterKeyCompatibilityProbe(
            ILogger<MasterKeyCompatibilityProbe> logger,
            IStorageBackend storage,
            string? connectionString = null)
        {
            _logger = logger;
            _storage = storage;
            _connectionString = connectionString;
        }

        /// <summary>
        /// Indicates whether existing cotton data async.
        /// </summary>
        public static async Task<bool> HasExistingCottonDataAsync(
            string? connectionString = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString ?? BuildConnectionStringFromEnvironment());
                await connection.OpenAsync(cancellationToken);
                return await HasExistingCottonDataAsync(connection, cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
            {
                return false;
            }
        }

        /// <summary>
        /// Validates async.
        /// </summary>
        public async Task<MasterKeyCompatibilityResult> ValidateAsync(
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString ?? BuildConnectionStringFromEnvironment());
                await connection.OpenAsync(cancellationToken);
                return await ValidateOpenDatabaseAsync(connection, encryptionSettings, mode, cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
            {
                return MasterKeyCompatibilityResult.Compatible(existingDataFound: false, evidenceFound: false);
            }
            catch (CryptographicException ex)
            {
                return FailInvalidMasterKey(ex);
            }
            catch (InvalidDataException ex)
            {
                return FailInvalidMasterKey(ex);
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

        private async Task<MasterKeyCompatibilityResult> ValidateOpenDatabaseAsync(
            NpgsqlConnection connection,
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken)
        {
            bool existingDataFound = await HasExistingCottonDataAsync(connection, cancellationToken);
            if (!existingDataFound)
            {
                return MasterKeyCompatibilityResult.Compatible(existingDataFound: false, evidenceFound: false);
            }

            using AesGcmStreamCipher cipher = MasterKeySentinelStore.CreateCipher(encryptionSettings);
            ProbeValidationState databaseProbe = await ValidateEncryptedDatabaseProbeAsync(
                connection,
                cipher,
                cancellationToken);
            if (databaseProbe == ProbeValidationState.Validated)
            {
                return MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: true);
            }

            if (_storage is IStorageBackendUsesEncryptedConfiguration)
            {
                return EvaluateFailedProbe(databaseProbe) ?? EvaluateMissingEvidence(mode);
            }

            ProbeValidationState storageProbe = await ValidateStorageChunkProbeAsync(connection, cipher, cancellationToken);
            if (storageProbe == ProbeValidationState.Validated)
            {
                return MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: true);
            }

            MasterKeyCompatibilityResult? failedProbe = EvaluateFailedProbe(databaseProbe)
                ?? EvaluateFailedProbe(storageProbe);
            if (failedProbe is not null)
            {
                return failedProbe;
            }

            return EvaluateMissingEvidence(mode);
        }

        private static MasterKeyCompatibilityResult? EvaluateFailedProbe(ProbeValidationState probe) =>
            probe == ProbeValidationState.FailedCandidates
                ? MasterKeyCompatibilityResult.Fail(
                    "Master key does not match existing encrypted Cotton data.",
                    existingDataFound: true,
                    evidenceFound: true)
                : null;

        private MasterKeyCompatibilityResult EvaluateMissingEvidence(MasterKeyCompatibilityMode mode)
        {
            return mode == MasterKeyCompatibilityMode.RequireEvidenceForExistingData
                ? MasterKeyCompatibilityResult.Fail(
                    "Existing Cotton data was found, but no encrypted data could be used to verify the submitted master key. Start Cotton once with COTTON_MASTER_KEY set to the original master key so it can seed the master-key sentinel safely.",
                    existingDataFound: true,
                    evidenceFound: false)
                : CompatibleWithoutEvidence();
        }

        private MasterKeyCompatibilityResult CompatibleWithoutEvidence()
        {
            _logger.LogInformation(
                "Existing Cotton data found, but no encrypted compatibility probe was available. Trusting the existing master-key sentinel.");
            return MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: false);
        }

        private MasterKeyCompatibilityResult FailInvalidMasterKey(Exception ex)
        {
            _logger.LogWarning(ex, "Submitted master key failed compatibility validation against existing encrypted Cotton data.");
            return MasterKeyCompatibilityResult.Fail("Master key does not match existing encrypted Cotton data.", evidenceFound: true);
        }

        internal static string BuildConnectionStringFromEnvironment()
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

        private async Task<ProbeValidationState> ValidateEncryptedDatabaseProbeAsync(
            NpgsqlConnection connection,
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            bool failedCandidate = false;
            foreach (EncryptedColumnProbe probe in EncryptedColumnProbes)
            {
                if (!await TableExistsAsync(connection, probe.TableName, cancellationToken)
                    || !await ColumnExistsAsync(connection, probe.TableName, probe.ColumnName, cancellationToken))
                {
                    continue;
                }

                IReadOnlyList<byte[]> candidates = await ReadEncryptedColumnProbeCandidatesAsync(
                    connection,
                    probe,
                    cancellationToken);
                foreach (byte[] encrypted in candidates)
                {
                    try
                    {
                        _ = cipher.Decrypt(encrypted);
                        return ProbeValidationState.Validated;
                    }
                    catch (Exception ex) when (IsEncryptedProbeFailure(ex))
                    {
                        failedCandidate = true;
                        _logger.LogDebug(
                            ex,
                            "Skipping encrypted database master-key probe candidate {Table}.{Column} after decrypt failure.",
                            probe.TableName,
                            probe.ColumnName);
                    }
                }
            }

            return failedCandidate
                ? ProbeValidationState.FailedCandidates
                : ProbeValidationState.NoCandidates;
        }

        private async Task<ProbeValidationState> ValidateStorageChunkProbeAsync(
            NpgsqlConnection connection,
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            if (!await TableExistsAsync(connection, "chunks", cancellationToken)
                || !await ColumnExistsAsync(connection, "chunks", "hash", cancellationToken))
            {
                return ProbeValidationState.NoCandidates;
            }

            bool failedCandidate = false;
            IReadOnlyList<string> storageKeys = await ReadCandidateChunkStorageKeysAsync(connection, cancellationToken);
            foreach (string storageKey in storageKeys)
            {
                if (!await _storage.ExistsAsync(storageKey))
                {
                    continue;
                }

                try
                {
                    await using Stream encrypted = await _storage.ReadAsync(storageKey);
                    await using Stream decrypted = await cipher.DecryptAsync(encrypted);
                    await decrypted.CopyToAsync(Stream.Null, cancellationToken);
                    return ProbeValidationState.Validated;
                }
                catch (Exception ex) when (IsEncryptedProbeFailure(ex))
                {
                    failedCandidate = true;
                    _logger.LogDebug(
                        ex,
                        "Skipping encrypted storage master-key probe candidate {StorageKey} after decrypt failure.",
                        storageKey);
                }
            }

            return failedCandidate
                ? ProbeValidationState.FailedCandidates
                : ProbeValidationState.NoCandidates;
        }

        private static async Task<IReadOnlyList<byte[]>> ReadEncryptedColumnProbeCandidatesAsync(
            NpgsqlConnection connection,
            EncryptedColumnProbe probe,
            CancellationToken cancellationToken)
        {
            string nonEmptyTextFilter = probe.Kind == EncryptedColumnKind.Base64String
                ? $" and {probe.ColumnName} <> ''"
                : string.Empty;
            string sql =
                $"select {probe.ColumnName} from public.{probe.TableName} where {probe.ColumnName} is not null{nonEmptyTextFilter} limit 8";

            var candidates = new List<byte[]>();
            await using var command = new NpgsqlCommand(sql, connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                object value = reader.GetValue(0);
                byte[]? encrypted = probe.Kind switch
                {
                    EncryptedColumnKind.Bytea => (byte[])value,
                    EncryptedColumnKind.Base64String => TryDecodeBase64((string)value),
                    _ => null
                };

                if (encrypted is not null)
                {
                    candidates.Add(encrypted);
                }
            }

            return candidates;
        }

        private static async Task<IReadOnlyList<string>> ReadCandidateChunkStorageKeysAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
        {
            bool hasStoredSize = await ColumnExistsAsync(connection, "chunks", "stored_size_bytes", cancellationToken);
            string orderBy = hasStoredSize
                ? " order by stored_size_bytes asc nulls last"
                : string.Empty;
            string sql = $"select hash from public.chunks where hash is not null{orderBy} limit 16";

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

        private static bool IsEncryptedProbeFailure(Exception ex) =>
            ex is CryptographicException
                or InvalidDataException
                or EndOfStreamException
                or IOException;

        private enum ProbeValidationState
        {
            NoCandidates,
            Validated,
            FailedCandidates
        }

        private record EncryptedColumnProbe(
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
