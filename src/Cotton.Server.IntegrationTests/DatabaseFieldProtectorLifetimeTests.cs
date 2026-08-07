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
using Npgsql;
using NUnit.Framework;

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

            string rawStoredSecret = await ReadRawSmtpPasswordAsync(instanceId);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(rawStoredSecret, Is.Not.EqualTo("first-secret"));
                Assert.DoesNotThrow(() => Convert.FromBase64String(rawStoredSecret));
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

        private async Task<string> ReadRawSmtpPasswordAsync(Guid instanceId)
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT smtp_password_encrypted FROM server_settings WHERE instance_id = @instanceId";
            command.Parameters.AddWithValue("instanceId", instanceId);
            object? result = await command.ExecuteScalarAsync();
            return result as string
                ?? throw new InvalidOperationException("Encrypted test field was not persisted.");
        }

        private static CottonEncryptionSettings CreateEncryptionSettings()
        {
            return ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "database-field-protector-key-001");
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
