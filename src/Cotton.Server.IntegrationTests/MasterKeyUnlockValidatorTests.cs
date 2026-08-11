// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Cotton.Storage.Backends;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class MasterKeyUnlockValidatorTests : IntegrationTestBase
    {
        private const string CorrectRootKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string WrongRootKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private string _storageBasePath = null!;

        [SetUp]
        public void SetUp()
        {
            _storageBasePath = Path.Combine(
                Path.GetTempPath(),
                "cotton-master-key-unlock-tests",
                Guid.NewGuid().ToString("N"));
            DbContext.Database.EnsureDeleted();
            DbContext.Database.Migrate();
        }

        [TearDown]
        public void TearDown()
        {
            TestDirectory.Delete(_storageBasePath);
        }

        [Test]
        public async Task ValidateAsync_UsesRegularContextAndAcceptsLocalSentinel()
        {
            CottonEncryptionSettings settings = CreateSettings(CorrectRootKey);
            FileSystemStorageBackend backend = CreateBackend();
            MasterKeySentinelStore sentinel = new(
                NullLogger<MasterKeySentinelStore>.Instance,
                backend);
            await sentinel.ValidateOrInitializeAsync(settings);
            MasterKeyUnlockValidator validator = CreateValidator();

            MasterKeySentinelResult result = await validator.ValidateAsync(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Created, Is.False);
            }
        }

        [Test]
        public async Task ValidateAsync_RejectsWrongKeyForLocalSentinel()
        {
            FileSystemStorageBackend backend = CreateBackend();
            MasterKeySentinelStore sentinel = new(
                NullLogger<MasterKeySentinelStore>.Instance,
                backend);
            await sentinel.ValidateOrInitializeAsync(CreateSettings(CorrectRootKey));
            MasterKeyUnlockValidator validator = CreateValidator();

            MasterKeyValidationException? exception = Assert.ThrowsAsync<MasterKeyValidationException>(
                async () => await validator.ValidateAsync(CreateSettings(WrongRootKey)));

            Assert.That(exception!.Message, Does.Contain("does not match"));
        }

        [Test]
        public async Task ValidateAsync_DoesNotAdoptExistingLocalDatabaseWithoutEvidence()
        {
            DbContext.ServerSettings.Add(CreateServerSettings(StorageType.Local));
            await DbContext.SaveChangesAsync();
            MasterKeyUnlockValidator validator = CreateValidator();

            MasterKeyValidationException? exception = Assert.ThrowsAsync<MasterKeyValidationException>(
                async () => await validator.ValidateAsync(CreateSettings(CorrectRootKey)));

            Assert.That(exception!.Message, Does.Contain("no integrity evidence"));
            Assert.That(
                await CreateBackend().ExistsAsync(MasterKeySentinelStore.SentinelStorageKey),
                Is.False);
        }

        [Test]
        public async Task ValidateAsync_RejectsWrongKeyBeforeConnectingToS3()
        {
            CottonEncryptionSettings settings = CreateSettings(CorrectRootKey);
            using DatabaseFieldProtector protector = new(
                settings,
                NullLogger<DatabaseFieldProtector>.Instance);
            await using CottonDbContext protectedDbContext = CreateProtectedDbContext(protector);
            CottonServerSettings serverSettings = CreateServerSettings(StorageType.S3);
            serverSettings.S3EndpointUrl = "https://unreachable.invalid";
            serverSettings.S3Region = "test";
            serverSettings.S3AccessKeyId = "access";
            serverSettings.S3SecretAccessKeyEncrypted = "secret";
            serverSettings.S3BucketName = "bucket";
            protectedDbContext.ServerSettings.Add(serverSettings);
            await protectedDbContext.SaveChangesAsync();
            MasterKeyUnlockValidator validator = CreateValidator();

            MasterKeyValidationException? exception = Assert.ThrowsAsync<MasterKeyValidationException>(
                async () => await validator.ValidateAsync(CreateSettings(WrongRootKey)));

            Assert.That(exception!.Message, Does.Contain("encrypted S3 configuration"));
        }

        [Test]
        public async Task ValidateAsync_KeepsS3AvailabilityFailureSeparateFromKeyRejection()
        {
            CottonEncryptionSettings settings = CreateSettings(CorrectRootKey);
            using DatabaseFieldProtector protector = new(
                settings,
                NullLogger<DatabaseFieldProtector>.Instance);
            await using CottonDbContext protectedDbContext = CreateProtectedDbContext(protector);
            CottonServerSettings serverSettings = CreateServerSettings(StorageType.S3);
            serverSettings.S3EndpointUrl = "http://127.0.0.1:1";
            serverSettings.S3Region = "test";
            serverSettings.S3AccessKeyId = "access";
            serverSettings.S3SecretAccessKeyEncrypted = "secret";
            serverSettings.S3BucketName = "bucket";
            protectedDbContext.ServerSettings.Add(serverSettings);
            await protectedDbContext.SaveChangesAsync();
            MasterKeyUnlockValidator validator = CreateValidator();

            Exception? exception = Assert.CatchAsync<Exception>(
                async () => await validator.ValidateAsync(settings));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception, Is.Not.TypeOf<MasterKeyValidationException>());
                Assert.That(exception is AmazonS3Exception or HttpRequestException, Is.True);
            }
        }

        [Test]
        public async Task HasExistingCottonDataAsync_UsesRegularDatabaseModel()
        {
            MasterKeyUnlockValidator validator = CreateValidator();
            bool before = await validator.HasExistingCottonDataAsync();
            DbContext.ServerSettings.Add(CreateServerSettings(StorageType.Local));
            await DbContext.SaveChangesAsync();

            bool after = await validator.HasExistingCottonDataAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(before, Is.False);
                Assert.That(after, Is.True);
            }
        }

        [Test]
        public async Task ValidateAsync_AllowsACompletelyUninitializedLocalInstance()
        {
            DbContext.Database.EnsureDeleted();
            MasterKeyUnlockValidator validator = CreateValidator();

            MasterKeySentinelResult result = await validator.ValidateAsync(
                CreateSettings(CorrectRootKey));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Created, Is.True);
                Assert.That(await validator.HasExistingCottonDataAsync(), Is.False);
            }
        }

        private MasterKeyUnlockValidator CreateValidator()
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            return new MasterKeyUnlockValidator(
                NullLoggerFactory.Instance,
                connectionString,
                _storageBasePath);
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

        private FileSystemStorageBackend CreateBackend()
        {
            return new FileSystemStorageBackend(
                NullLogger<FileSystemStorageBackend>.Instance,
                _storageBasePath);
        }

        private static CottonEncryptionSettings CreateSettings(string rootKey)
        {
            return ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootKey);
        }

        private static CottonServerSettings CreateServerSettings(StorageType storageType)
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
                StorageType = storageType,
                InstanceId = Guid.NewGuid(),
                PublicBaseUrl = "http://localhost",
                ServerUsage = [ServerUsage.Other],
                StorageSpaceMode = StorageSpaceMode.Optimal,
                GeoIpLookupMode = GeoIpLookupMode.Disabled
            };
        }
    }
}
