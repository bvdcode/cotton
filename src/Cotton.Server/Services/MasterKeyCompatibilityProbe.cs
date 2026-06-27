// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Crypto;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents master key compatibility probe.
    /// </summary>
    public class MasterKeyCompatibilityProbe : IMasterKeyCompatibilityProbe
    {
        private static readonly EncryptedBytesProbe[] EncryptedBytesProbes =
        [
            new(
                nameof(MasterKeyProbeUser.TotpSecretEncrypted),
                dbContext => dbContext.Users
                    .AsNoTracking()
                    .Where(x => x.TotpSecretEncrypted != null)
                    .Select(x => x.TotpSecretEncrypted!)),
            new(
                nameof(MasterKeyProbeUser.AvatarHashEncrypted),
                dbContext => dbContext.Users
                    .AsNoTracking()
                    .Where(x => x.AvatarHashEncrypted != null)
                    .Select(x => x.AvatarHashEncrypted!)),
            new(
                nameof(MasterKeyProbeFileManifest.SmallFilePreviewHashEncrypted),
                dbContext => dbContext.FileManifests
                    .AsNoTracking()
                    .Where(x => x.SmallFilePreviewHashEncrypted != null)
                    .Select(x => x.SmallFilePreviewHashEncrypted!))
        ];

        private static readonly EncryptedTextProbe[] EncryptedTextProbes =
        [
            new(
                nameof(MasterKeyProbeServerSettings.CloudServicesTokenEncrypted),
                dbContext => dbContext.ServerSettings
                    .AsNoTracking()
                    .Where(x => x.CloudServicesTokenEncrypted != null && x.CloudServicesTokenEncrypted != string.Empty)
                    .Select(x => x.CloudServicesTokenEncrypted!)),
            new(
                nameof(MasterKeyProbeServerSettings.OidcClientSecretEncrypted),
                dbContext => dbContext.ServerSettings
                    .AsNoTracking()
                    .Where(x => x.OidcClientSecretEncrypted != null && x.OidcClientSecretEncrypted != string.Empty)
                    .Select(x => x.OidcClientSecretEncrypted!)),
            new(
                nameof(MasterKeyProbeServerSettings.S3SecretAccessKeyEncrypted),
                dbContext => dbContext.ServerSettings
                    .AsNoTracking()
                    .Where(x => x.S3SecretAccessKeyEncrypted != null && x.S3SecretAccessKeyEncrypted != string.Empty)
                    .Select(x => x.S3SecretAccessKeyEncrypted!)),
            new(
                nameof(MasterKeyProbeServerSettings.SmtpPasswordEncrypted),
                dbContext => dbContext.ServerSettings
                    .AsNoTracking()
                    .Where(x => x.SmtpPasswordEncrypted != null && x.SmtpPasswordEncrypted != string.Empty)
                    .Select(x => x.SmtpPasswordEncrypted!)),
            new(
                nameof(MasterKeyProbeServerSettings.FcmServiceAccountJsonEncrypted),
                dbContext => dbContext.ServerSettings
                    .AsNoTracking()
                    .Where(x => x.FcmServiceAccountJsonEncrypted != null && x.FcmServiceAccountJsonEncrypted != string.Empty)
                    .Select(x => x.FcmServiceAccountJsonEncrypted!)),
            new(
                nameof(MasterKeyProbeOidcProvider.ClientSecretEncrypted),
                dbContext => dbContext.OidcProviders
                    .AsNoTracking()
                    .Where(x => x.ClientSecretEncrypted != null && x.ClientSecretEncrypted != string.Empty)
                    .Select(x => x.ClientSecretEncrypted!)),
            new(
                nameof(MasterKeyProbeOidcLoginState.CodeVerifierEncrypted),
                dbContext => dbContext.OidcLoginStates
                    .AsNoTracking()
                    .Where(x => x.CodeVerifierEncrypted != null && x.CodeVerifierEncrypted != string.Empty)
                    .Select(x => x.CodeVerifierEncrypted!)),
            new(
                nameof(MasterKeyProbeOidcLoginState.NonceEncrypted),
                dbContext => dbContext.OidcLoginStates
                    .AsNoTracking()
                    .Where(x => x.NonceEncrypted != null && x.NonceEncrypted != string.Empty)
                    .Select(x => x.NonceEncrypted!))
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
                await using MasterKeyProbeDbContext dbContext = CreateDbContext(connectionString);
                return await HasExistingCottonDataAsync(dbContext, cancellationToken);
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
                await using MasterKeyProbeDbContext dbContext = CreateDbContext(_connectionString);
                return await ValidateOpenDatabaseAsync(dbContext, encryptionSettings, mode, cancellationToken);
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
            MasterKeyProbeDbContext dbContext,
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken)
        {
            bool existingDataFound = await HasExistingCottonDataAsync(dbContext, cancellationToken);
            if (!existingDataFound)
            {
                return MasterKeyCompatibilityResult.Compatible(existingDataFound: false, evidenceFound: false);
            }

            using AesGcmStreamCipher cipher = MasterKeySentinelStore.CreateCipher(encryptionSettings);
            ProbeValidationState databaseProbe = await ValidateEncryptedDatabaseProbeAsync(
                dbContext,
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

            ProbeValidationState storageProbe = await ValidateStorageChunkProbeAsync(dbContext, cipher, cancellationToken);
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

        internal static MasterKeyProbeDbContext CreateDbContext(string? connectionString = null)
        {
            var options = new DbContextOptionsBuilder<MasterKeyProbeDbContext>()
                .UseNpgsql(connectionString ?? BuildConnectionStringFromEnvironment())
                .Options;
            return new MasterKeyProbeDbContext(options);
        }

        private static async Task<bool> HasExistingCottonDataAsync(
            MasterKeyProbeDbContext dbContext,
            CancellationToken cancellationToken)
        {
            return await EntityHasRowsAsync(dbContext.Users, cancellationToken)
                || await EntityHasRowsAsync(dbContext.Nodes, cancellationToken)
                || await EntityHasRowsAsync(dbContext.FileManifests, cancellationToken)
                || await EntityHasRowsAsync(dbContext.Chunks, cancellationToken)
                || await EntityHasRowsAsync(dbContext.ServerSettings, cancellationToken);
        }

        private async Task<ProbeValidationState> ValidateEncryptedDatabaseProbeAsync(
            MasterKeyProbeDbContext dbContext,
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            bool failedCandidate = false;
            foreach (EncryptedBytesProbe probe in EncryptedBytesProbes)
            {
                IReadOnlyList<byte[]> candidates = await ReadEncryptedBytesProbeCandidatesAsync(
                    dbContext,
                    probe,
                    cancellationToken);
                CandidateValidationOutcome outcome = await ValidateEncryptedCandidatesAsync(
                    cipher,
                    probe.Description,
                    candidates,
                    cancellationToken);
                failedCandidate |= outcome.FailedCandidate;
                if (outcome.Validated)
                {
                    return ProbeValidationState.Validated;
                }
            }

            foreach (EncryptedTextProbe probe in EncryptedTextProbes)
            {
                IReadOnlyList<byte[]> candidates = await ReadEncryptedTextProbeCandidatesAsync(
                    dbContext,
                    probe,
                    cancellationToken);
                CandidateValidationOutcome outcome = await ValidateEncryptedCandidatesAsync(
                    cipher,
                    probe.Description,
                    candidates,
                    cancellationToken);
                failedCandidate |= outcome.FailedCandidate;
                if (outcome.Validated)
                {
                    return ProbeValidationState.Validated;
                }
            }

            return failedCandidate
                ? ProbeValidationState.FailedCandidates
                : ProbeValidationState.NoCandidates;
        }

        private async Task<ProbeValidationState> ValidateStorageChunkProbeAsync(
            MasterKeyProbeDbContext dbContext,
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            bool failedCandidate = false;
            IReadOnlyList<string> storageKeys = await ReadCandidateChunkStorageKeysAsync(dbContext, cancellationToken);
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

        private async Task<CandidateValidationOutcome> ValidateEncryptedCandidatesAsync(
            AesGcmStreamCipher cipher,
            string description,
            IReadOnlyList<byte[]> candidates,
            CancellationToken cancellationToken)
        {
            bool failedCandidate = false;
            foreach (byte[] encrypted in candidates)
            {
                try
                {
                    _ = await cipher.DecryptAsync(encrypted, cancellationToken);
                    return new CandidateValidationOutcome(Validated: true, FailedCandidate: failedCandidate);
                }
                catch (Exception ex) when (IsEncryptedProbeFailure(ex))
                {
                    failedCandidate = true;
                    _logger.LogDebug(
                        ex,
                        "Skipping encrypted database master-key probe candidate {ProbeDescription} after decrypt failure.",
                        description);
                }
            }

            return new CandidateValidationOutcome(Validated: false, FailedCandidate: failedCandidate);
        }

        private static async Task<IReadOnlyList<byte[]>> ReadEncryptedBytesProbeCandidatesAsync(
            MasterKeyProbeDbContext dbContext,
            EncryptedBytesProbe probe,
            CancellationToken cancellationToken)
        {
            try
            {
                return await probe.QueryFactory(dbContext)
                    .Take(8)
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsMissingDatabaseShape(ex))
            {
                return [];
            }
        }

        private static async Task<IReadOnlyList<byte[]>> ReadEncryptedTextProbeCandidatesAsync(
            MasterKeyProbeDbContext dbContext,
            EncryptedTextProbe probe,
            CancellationToken cancellationToken)
        {
            try
            {
                List<string> values = await probe.QueryFactory(dbContext)
                    .Take(8)
                    .ToListAsync(cancellationToken);
                return [.. values.Select(TryDecodeBase64).OfType<byte[]>()];
            }
            catch (PostgresException ex) when (IsMissingDatabaseShape(ex))
            {
                return [];
            }
        }

        private static async Task<IReadOnlyList<string>> ReadCandidateChunkStorageKeysAsync(
            MasterKeyProbeDbContext dbContext,
            CancellationToken cancellationToken)
        {
            try
            {
                List<byte[]> hashes = await dbContext.Chunks
                    .AsNoTracking()
                    .Select(x => x.Hash)
                    .Take(16)
                    .ToListAsync(cancellationToken);
                return [.. hashes.Select(Hasher.ToHexStringHash)];
            }
            catch (PostgresException ex) when (IsMissingDatabaseShape(ex))
            {
                return [];
            }
        }

        private static async Task<bool> EntityHasRowsAsync<TEntity>(
            IQueryable<TEntity> query,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            try
            {
                return await query.AsNoTracking().AnyAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsMissingDatabaseShape(ex))
            {
                return false;
            }
        }

        internal static bool IsMissingDatabaseShape(PostgresException ex) =>
            ex.SqlState == PostgresErrorCodes.UndefinedTable
            || ex.SqlState == PostgresErrorCodes.UndefinedColumn
            || ex.SqlState == PostgresErrorCodes.UndefinedObject;

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

        private record EncryptedBytesProbe(
            string Description,
            Func<MasterKeyProbeDbContext, IQueryable<byte[]>> QueryFactory);

        private record EncryptedTextProbe(
            string Description,
            Func<MasterKeyProbeDbContext, IQueryable<string>> QueryFactory);

        private record CandidateValidationOutcome(bool Validated, bool FailedCandidate);
    }
}
