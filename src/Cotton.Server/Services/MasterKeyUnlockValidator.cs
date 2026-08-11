// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Validates a browser-submitted master key through the regular database and storage models.
    /// </summary>
    internal class MasterKeyUnlockValidator(
        ILoggerFactory _loggerFactory,
        string? _connectionString = null,
        string? _localStorageBasePath = null)
    {
        private readonly ILogger<MasterKeyUnlockValidator> _logger =
            _loggerFactory.CreateLogger<MasterKeyUnlockValidator>();

        public async Task<MasterKeySentinelResult> ValidateAsync(
            CottonEncryptionSettings encryptionSettings,
            CancellationToken cancellationToken = default)
        {
            DbContextOptions<CottonDbContext> options = CreateDbContextOptions();
            using DatabaseFieldProtector fieldProtector = new(
                encryptionSettings,
                _loggerFactory.CreateLogger<DatabaseFieldProtector>());
            await using CottonDbContext dbContext = new(options, fieldProtector);

            StorageType storageType = await LoadStorageTypeAsync(dbContext, cancellationToken);
            S3Provider? s3Provider = null;
            if (storageType == StorageType.S3)
            {
                s3Provider = new S3Provider(
                    await LoadS3ConfigurationAsync(dbContext, cancellationToken));
            }

            using (s3Provider)
            {
                return await ValidateBackendAsync(
                    dbContext,
                    encryptionSettings,
                    storageType,
                    s3Provider,
                    cancellationToken);
            }
        }

        private async Task<MasterKeySentinelResult> ValidateBackendAsync(
            CottonDbContext dbContext,
            CottonEncryptionSettings encryptionSettings,
            StorageType storageType,
            S3Provider? s3Provider,
            CancellationToken cancellationToken)
        {
            StorageBackendFactory backendFactory = new(
                _loggerFactory.CreateLogger<FileSystemStorageBackend>(),
                _loggerFactory.CreateLogger<S3StorageBackend>());
            IStorageBackend backend = backendFactory.Create(
                storageType,
                s3Provider,
                _localStorageBasePath);

            using AesGcmStreamCipher cipher = StreamCipherFactory.Create(encryptionSettings);
            using DatabaseIntegrityKeyProvider keyProvider = new(encryptionSettings);
            MasterKeyValidator validator = new(
                cipher,
                encryptionSettings,
                dbContext,
                CreateIntegrityVerifier(keyProvider),
                _loggerFactory.CreateLogger<MasterKeySentinelStore>(),
                _loggerFactory.CreateLogger<MasterKeyValidator>());
            return await validator.ValidateAsync(
                backend,
                encryptedConfigurationValidated: storageType == StorageType.S3,
                cancellationToken);
        }

        public async Task<bool> HasExistingCottonDataAsync(CancellationToken cancellationToken = default)
        {
            await using CottonDbContext dbContext = new(CreateDbContextOptions());
            return await EntityHasRowsAsync(dbContext.Users, cancellationToken)
                || await EntityHasRowsAsync(dbContext.Nodes, cancellationToken)
                || await EntityHasRowsAsync(dbContext.FileManifests, cancellationToken)
                || await EntityHasRowsAsync(dbContext.Chunks, cancellationToken)
                || await EntityHasRowsAsync(dbContext.ServerSettings, cancellationToken);
        }

        private DatabaseIntegrityVerifier CreateIntegrityVerifier(DatabaseIntegrityKeyProvider keyProvider)
        {
            UserIntegrityDescriptor descriptor = new();
            DatabaseIntegrityProtector protector = new(keyProvider);
            return new DatabaseIntegrityVerifier(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance,
                _loggerFactory.CreateLogger<DatabaseIntegrityVerifier>());
        }

        private async Task<StorageType> LoadStorageTypeAsync(
            CottonDbContext dbContext,
            CancellationToken cancellationToken)
        {
            try
            {
                StorageType? storageType = await dbContext.ServerSettings
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => (StorageType?)x.StorageType)
                    .FirstOrDefaultAsync(cancellationToken);
                return storageType ?? StorageType.Local;
            }
            catch (PostgresException ex) when (IsMissingDatabaseShape(ex))
            {
                _logger.LogDebug(
                    ex,
                    "Storage settings are not available before database initialization; using local storage.");
                return StorageType.Local;
            }
        }

        private async Task<S3Config> LoadS3ConfigurationAsync(
            CottonDbContext dbContext,
            CancellationToken cancellationToken)
        {
            try
            {
                S3Config? configuration = await dbContext.ServerSettings
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new S3Config
                    {
                        Endpoint = x.S3EndpointUrl!,
                        Region = x.S3Region!,
                        AccessKey = x.S3AccessKeyId!,
                        SecretKey = x.S3SecretAccessKeyEncrypted!,
                        Bucket = x.S3BucketName!
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                return configuration
                    ?? throw new InvalidOperationException("S3 storage is selected, but server settings are missing.");
            }
            catch (Exception ex) when (ex is FormatException
                or CryptographicException
                or InvalidDataException
                or EndOfStreamException)
            {
                _logger.LogWarning(
                    ex,
                    "Submitted master key could not decrypt the S3 configuration.");
                throw new MasterKeyValidationException(
                    "Master key does not match the encrypted S3 configuration.",
                    ex);
            }
        }

        private async Task<bool> EntityHasRowsAsync<TEntity>(
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
                _logger.LogDebug(
                    ex,
                    "Database object is not available while checking whether Cotton has existing data.");
                return false;
            }
        }

        private DbContextOptions<CottonDbContext> CreateDbContextOptions()
        {
            return new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql(_connectionString ?? BuildConnectionStringFromEnvironment())
                .EnableServiceProviderCaching(false)
                .Options;
        }

        private static string BuildConnectionStringFromEnvironment()
        {
            string postgresPort = Environment.GetEnvironmentVariable("COTTON_PG_PORT") ?? "5432";
            if (!int.TryParse(postgresPort, out int port))
            {
                throw new InvalidOperationException("COTTON_PG_PORT must be a valid integer.");
            }

            NpgsqlConnectionStringBuilder builder = new()
            {
                Host = Environment.GetEnvironmentVariable("COTTON_PG_HOST") ?? "localhost",
                Port = port,
                Database = Environment.GetEnvironmentVariable("COTTON_PG_DATABASE") ?? "cotton_dev",
                Username = Environment.GetEnvironmentVariable("COTTON_PG_USERNAME") ?? "postgres",
                Password = Environment.GetEnvironmentVariable("COTTON_PG_PASSWORD") ?? "postgres"
            };
            return builder.ConnectionString;
        }

        private static bool IsMissingDatabaseShape(PostgresException ex)
        {
            return ex.SqlState == PostgresErrorCodes.InvalidCatalogName
                || ex.SqlState == PostgresErrorCodes.UndefinedTable
                || ex.SqlState == PostgresErrorCodes.UndefinedColumn
                || ex.SqlState == PostgresErrorCodes.UndefinedObject;
        }
    }
}
