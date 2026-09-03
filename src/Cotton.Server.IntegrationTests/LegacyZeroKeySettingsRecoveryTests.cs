// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class LegacyZeroKeySettingsRecoveryTests : IntegrationTestBase
    {
        [SetUp]
        public void SetUp()
        {
            DbContext.Database.EnsureDeleted();
            DbContext.Database.Migrate();
        }

        [Test]
        public async Task RepairAsync_ReprotectsLegacySettingsAndDisablesFallback()
        {
            CottonEncryptionSettings currentSettings =
                ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                    "database-field-protector-key-001");
            CottonEncryptionSettings legacySettings = CreateLegacySettings(currentSettings);
            Guid instanceId = Guid.NewGuid();

            await using (ServiceProvider services = CreateServiceProvider(legacySettings))
            await using (AsyncServiceScope scope = services.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                dbContext.ServerSettings.Add(CreateServerSettings(instanceId));
                await dbContext.SaveChangesAsync();
            }

            await using ServiceProvider currentServices = CreateServiceProvider(currentSettings);
            await using AsyncServiceScope currentScope = currentServices.CreateAsyncScope();
            CottonDbContext currentDbContext = currentScope.ServiceProvider
                .GetRequiredService<CottonDbContext>();
            CottonServerSettings recovered = await currentDbContext.ServerSettings
                .SingleAsync(x => x.InstanceId == instanceId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recovered.SmtpPasswordEncrypted, Is.EqualTo("smtp-secret"));
                Assert.That(recovered.S3SecretAccessKeyEncrypted, Is.EqualTo("s3-secret"));
            }

#pragma warning disable CS0618 // TEMPORARY 0.5 RECOVERY: remove with the recovery service.
            LegacyZeroKeySettingsRecovery recovery = currentServices
                .GetRequiredService<LegacyZeroKeySettingsRecovery>();
            await recovery.RepairAsync(currentScope.ServiceProvider);
#pragma warning restore CS0618

            await using ServiceProvider normalOnlyServices = CreateServiceProvider(
                currentSettings,
                includeRecovery: false);
            await using AsyncServiceScope normalOnlyScope = normalOnlyServices.CreateAsyncScope();
            CottonDbContext normalOnlyDbContext = normalOnlyScope.ServiceProvider
                .GetRequiredService<CottonDbContext>();
            CottonServerSettings persisted = await normalOnlyDbContext.ServerSettings
                .SingleAsync(x => x.InstanceId == instanceId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(persisted.SmtpPasswordEncrypted, Is.EqualTo("smtp-secret"));
                Assert.That(persisted.S3SecretAccessKeyEncrypted, Is.EqualTo("s3-secret"));
            }

            using AesGcmStreamCipher zeroKeyCipher = new(
                new byte[AesGcmStreamCipher.KeySize],
                currentSettings.MasterEncryptionKeyId,
                threads: 1);
            string lateLegacyValue = Convert.ToBase64String(
                await zeroKeyCipher.EncryptStringAsync("late-legacy-value"));
            IDatabaseFieldProtector activeProtector = currentServices
                .GetRequiredService<IDatabaseFieldProtector>();

            Assert.Throws<AuthenticationTagMismatchException>(() =>
                activeProtector.Unprotect(lateLegacyValue));
        }

        private ServiceProvider CreateServiceProvider(
            CottonEncryptionSettings settings,
            bool includeRecovery = true)
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton(settings);
            if (includeRecovery)
            {
                services.AddStreamCipher();
            }
            else
            {
                services.AddSingleton<DatabaseFieldProtector>();
                services.AddSingleton<IDatabaseFieldProtector>(sp =>
                    sp.GetRequiredService<DatabaseFieldProtector>());
            }
            services.AddDbContext<CottonDbContext>(options => options.UseNpgsql(connectionString));
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        private static CottonEncryptionSettings CreateLegacySettings(
            CottonEncryptionSettings currentSettings)
        {
            return new()
            {
                EncryptionThreads = 1,
                MasterEncryptionKey = Convert.ToBase64String(
                    new byte[AesGcmStreamCipher.KeySize]),
                MasterEncryptionKeyId = currentSettings.MasterEncryptionKeyId,
                Pepper = currentSettings.Pepper,
            };
        }

        private static CottonServerSettings CreateServerSettings(Guid instanceId)
        {
            return new()
            {
                AllowCrossUserDeduplication = false,
                AllowGlobalIndexing = false,
                CipherChunkSizeBytes = AesGcmStreamCipher.DefaultChunkSize,
                CompressionLevel = 3,
                ComputionMode = ComputionMode.Local,
                EmailMode = EmailMode.Cloud,
                EncryptionThreads = 1,
                GeoIpLookupMode = GeoIpLookupMode.Disabled,
                InstanceId = instanceId,
                MaxChunkSizeBytes = 1024 * 1024,
                PublicBaseUrl = "http://localhost",
                S3SecretAccessKeyEncrypted = "s3-secret",
                ServerUsage = [ServerUsage.Other],
                SessionTimeoutHours = 30 * 24,
                SmtpPasswordEncrypted = "smtp-secret",
                StorageSpaceMode = StorageSpaceMode.Optimal,
                StorageType = StorageType.Local,
                TelemetryEnabled = false,
                Timezone = "UTC",
                TotpMaxFailedAttempts = 5,
            };
        }
    }
}
