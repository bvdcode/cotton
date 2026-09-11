// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class MasterKeyUnlockValidatorS3Tests : IntegrationTestBase
    {
        private const string RootKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private S3Config _configuration = null!;
        private S3Provider _s3Provider = null!;
        private IStorageBackend _backend = null!;

        [SetUp]
        public async Task SetUp()
        {
            _configuration = LoadConfiguration();
            if (!IsComplete(_configuration))
            {
                Assert.Ignore("S3 test configuration is not available.");
            }

            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.Database.MigrateAsync();
            _s3Provider = new S3Provider(_configuration);
            StorageBackendFactory factory = new(
                NullLogger<FileSystemStorageBackend>.Instance,
                NullLogger<S3StorageBackend>.Instance);
            _backend = factory.Create(StorageType.S3, _s3Provider);
            await _backend.DeleteAsync(MasterKeySentinelStore.SentinelStorageKey);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_backend is not null)
            {
                await _backend.DeleteAsync(MasterKeySentinelStore.SentinelStorageKey);
            }

            _s3Provider?.Dispose();
        }

        [Test]
        [Explicit("Requires COTTON_TEST_S3_* credentials.")]
        public async Task ValidateAsync_DecryptsConfigurationAndCreatesS3Sentinel()
        {
            CottonEncryptionSettings encryptionSettings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(RootKey);
            using DatabaseFieldProtector protector = new(
                encryptionSettings,
                NullLogger<DatabaseFieldProtector>.Instance);
            await using CottonDbContext protectedDbContext = CreateProtectedDbContext(protector);
            protectedDbContext.ServerSettings.Add(CreateServerSettings());
            await protectedDbContext.SaveChangesAsync();
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            MasterKeyUnlockValidator validator = new(
                NullLoggerFactory.Instance,
                connectionString);

            MasterKeySentinelResult result = await validator.ValidateAsync(encryptionSettings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Created, Is.True);
                Assert.That(
                    await _backend.ExistsAsync(MasterKeySentinelStore.SentinelStorageKey),
                    Is.True);
            }
        }

        private CottonDbContext CreateProtectedDbContext(IDatabaseFieldProtector protector)
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql(connectionString)
                .EnableServiceProviderCaching(false)
                .Options;
            return new CottonDbContext(options, protector);
        }

        private CottonServerSettings CreateServerSettings()
        {
            return new CottonServerSettings
            {
                AllowCrossUserDeduplication = false,
                AllowGlobalIndexing = false,
                CipherChunkSizeBytes = AesGcmStreamCipher.DefaultChunkSize,
                CompressionLevel = 3,
                EncryptionThreads = 1,
                MaxChunkSizeBytes = 1024 * 1024,
                SessionTimeoutHours = 30 * 24,
                TelemetryEnabled = false,
                Timezone = "UTC",
                TotpMaxFailedAttempts = 5,
                EmailMode = EmailMode.None,
                ComputionMode = ComputionMode.Local,
                StorageType = StorageType.S3,
                InstanceId = Guid.NewGuid(),
                PublicBaseUrl = "http://localhost",
                ServerUsage = [ServerUsage.Other],
                StorageSpaceMode = StorageSpaceMode.Optimal,
                GeoIpLookupMode = GeoIpLookupMode.Disabled,
                S3EndpointUrl = _configuration.Endpoint,
                S3Region = _configuration.Region,
                S3AccessKeyId = _configuration.AccessKey,
                S3SecretAccessKeyEncrypted = _configuration.SecretKey,
                S3BucketName = _configuration.Bucket
            };
        }

        private static S3Config LoadConfiguration()
        {
            return new S3Config
            {
                AccessKey = Environment.GetEnvironmentVariable("COTTON_TEST_S3_ACCESS_KEY") ?? string.Empty,
                SecretKey = Environment.GetEnvironmentVariable("COTTON_TEST_S3_SECRET_KEY") ?? string.Empty,
                Endpoint = Environment.GetEnvironmentVariable("COTTON_TEST_S3_ENDPOINT") ?? string.Empty,
                Bucket = Environment.GetEnvironmentVariable("COTTON_TEST_S3_BUCKET") ?? string.Empty,
                Region = Environment.GetEnvironmentVariable("COTTON_TEST_S3_REGION") ?? string.Empty
            };
        }

        private static bool IsComplete(S3Config configuration)
        {
            return !string.IsNullOrWhiteSpace(configuration.AccessKey)
                && !string.IsNullOrWhiteSpace(configuration.SecretKey)
                && !string.IsNullOrWhiteSpace(configuration.Endpoint)
                && !string.IsNullOrWhiteSpace(configuration.Bucket)
                && !string.IsNullOrWhiteSpace(configuration.Region);
        }
    }
}
