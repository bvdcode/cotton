// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.IntegrationTests.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseFieldProtectorLifetimeTests : IntegrationTestBase
    {
        [SetUp]
        public void SetUp()
        {
            DbContext.Database.EnsureDeleted();
            DbContext.Database.Migrate();
        }

        [Test]
        public async Task EncryptedFields_RemainUsableAfterCreatingScopeIsDisposed()
        {
            CottonEncryptionSettings settings = CreateEncryptionSettings();
            await using ServiceProvider services = CreateServiceProvider(settings);
            Guid instanceId = Guid.NewGuid();

            await using (AsyncServiceScope scope = services.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                dbContext.ServerSettings.Add(CreateServerSettings(instanceId, "first-secret"));
                await dbContext.SaveChangesAsync();
            }

            await using (AsyncServiceScope scope = services.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                CottonServerSettings stored = await dbContext.ServerSettings
                    .SingleAsync(x => x.InstanceId == instanceId);

                Assert.That(stored.SmtpPasswordEncrypted, Is.EqualTo("first-secret"));

                stored.SmtpPasswordEncrypted = "updated-secret";
                await dbContext.SaveChangesAsync();
            }

            await using (AsyncServiceScope scope = services.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                string? storedSecret = await dbContext.ServerSettings
                    .Where(x => x.InstanceId == instanceId)
                    .Select(x => x.SmtpPasswordEncrypted)
                    .SingleAsync();

                Assert.That(storedSecret, Is.EqualTo("updated-secret"));
            }
        }

        [Test]
        public async Task EncryptedFields_UseProtectorFromCurrentServiceProvider()
        {
            CottonEncryptionSettings settings = CreateEncryptionSettings();
            Guid instanceId = Guid.NewGuid();

            await using (ServiceProvider firstServices = CreateServiceProvider(settings))
            await using (AsyncServiceScope scope = firstServices.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                dbContext.ServerSettings.Add(CreateServerSettings(instanceId, "first-provider-secret"));
                await dbContext.SaveChangesAsync();
            }

            await using ServiceProvider secondServices = CreateServiceProvider(settings);
            await using AsyncServiceScope secondScope = secondServices.CreateAsyncScope();
            CottonDbContext secondDbContext = secondScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            string? storedSecret = await secondDbContext.ServerSettings
                .Where(settingsRow => settingsRow.InstanceId == instanceId)
                .Select(settingsRow => settingsRow.SmtpPasswordEncrypted)
                .SingleAsync();

            Assert.That(storedSecret, Is.EqualTo("first-provider-secret"));
        }

        [Test]
        public async Task EncryptedFields_DoNotReuseProtectorFromDifferentServiceProvider()
        {
            CottonEncryptionSettings firstSettings = CreateEncryptionSettings();
            CottonEncryptionSettings secondSettings = CreateEncryptionSettings(
                "database-field-protector-key-002");
            Guid instanceId = Guid.NewGuid();

            await using ServiceProvider firstServices = CreateServiceProvider(firstSettings);
            await using (AsyncServiceScope scope = firstServices.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                dbContext.ServerSettings.Add(CreateServerSettings(instanceId, "isolated-secret"));
                await dbContext.SaveChangesAsync();
            }

            await using ServiceProvider secondServices = CreateServiceProvider(secondSettings);
            await using AsyncServiceScope secondScope = secondServices.CreateAsyncScope();
            CottonDbContext secondDbContext = secondScope.ServiceProvider.GetRequiredService<CottonDbContext>();

            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () =>
                await secondDbContext.ServerSettings
                    .Where(settingsRow => settingsRow.InstanceId == instanceId)
                    .Select(settingsRow => settingsRow.SmtpPasswordEncrypted)
                    .SingleAsync());
        }

        [Test]
        public async Task EncryptedFields_DoNotReuseRawContextModel()
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using (CottonDbContext rawDbContext = new(options))
            {
                await rawDbContext.ServerSettings.CountAsync();
            }

            CottonEncryptionSettings settings = CreateEncryptionSettings();
            await using ServiceProvider services = CreateServiceProvider(settings);
            IDatabaseFieldProtector protector = services.GetRequiredService<IDatabaseFieldProtector>();
            await using CottonDbContext protectedDbContext = new(options, protector);
            Guid instanceId = Guid.NewGuid();
            protectedDbContext.ServerSettings.Add(CreateServerSettings(instanceId, "protected-secret"));

            await protectedDbContext.SaveChangesAsync();

            string? storedSecret = await protectedDbContext.ServerSettings
                .Where(settingsRow => settingsRow.InstanceId == instanceId)
                .Select(settingsRow => settingsRow.SmtpPasswordEncrypted)
                .SingleAsync();
            Assert.That(storedSecret, Is.EqualTo("protected-secret"));
        }

        private ServiceProvider CreateServiceProvider(CottonEncryptionSettings settings)
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton(settings);
            services.AddStreamCipher();
            services.AddDbContext<CottonDbContext>(options => options.UseNpgsql(connectionString));
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        private static CottonEncryptionSettings CreateEncryptionSettings(
            string masterKey = "database-field-protector-key-001")
        {
            return ConfigurationBuilderExtensions.DeriveEncryptionSettings(masterKey);
        }

        private static CottonServerSettings CreateServerSettings(Guid instanceId, string smtpPassword)
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
                StorageType = StorageType.Local,
                InstanceId = instanceId,
                PublicBaseUrl = "http://localhost",
                ServerUsage = [ServerUsage.Other],
                StorageSpaceMode = StorageSpaceMode.Optimal,
                GeoIpLookupMode = GeoIpLookupMode.Disabled,
                SmtpPasswordEncrypted = smtpPassword,
            };
        }
    }
}
