// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public partial class LegacyZeroKeySettingsRecoveryTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public async Task RepairAsync_ReprotectsOidcSecretAndPreservesIntegrity(bool hasServerSettings)
        {
            CottonEncryptionSettings currentSettings = ConfigurationBuilderExtensions
                .DeriveEncryptionSettings("database-field-protector-key-001");
            CottonEncryptionSettings legacySettings = CreateLegacySettings(currentSettings);
            await using ServiceProvider legacyServices = CreateServiceProvider(
                legacySettings, includeRecovery: false, integritySettings: currentSettings);
            Guid legacyProviderId = await CreateProviderAsync(legacyServices, "legacy");

            await using ServiceProvider currentServices = CreateServiceProvider(currentSettings);
            Guid currentProviderId = await CreateProviderAsync(currentServices, "current");
            await using (AsyncServiceScope scope = currentServices.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                if (hasServerSettings)
                {
                    dbContext.ServerSettings.Add(CreateServerSettings(Guid.NewGuid()));
                    await dbContext.SaveChangesAsync();
                }

                await RepairAsync(currentServices, scope.ServiceProvider);
            }

            await AssertProviderAsync(currentServices, legacyProviderId, "legacy");
            await AssertProviderAsync(currentServices, currentProviderId, "current");
            await using ServiceProvider normalServices = CreateServiceProvider(currentSettings, includeRecovery: false);
            await AssertProviderAsync(normalServices, legacyProviderId, "legacy");
            await AssertProviderAsync(normalServices, currentProviderId, "current");

            await using (AsyncServiceScope scope = currentServices.CreateAsyncScope())
            {
                await RepairAsync(currentServices, scope.ServiceProvider);
            }

            await AssertProviderAsync(normalServices, legacyProviderId, "legacy");
            await CreateProviderAsync(legacyServices, "late-legacy");
            await using AsyncServiceScope lateScope = currentServices.CreateAsyncScope();
            CottonDbContext lateDbContext = lateScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () =>
                await lateDbContext.OidcProviders.SingleAsync(x => x.Slug == "late-legacy"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task RepairAsync_RejectsInvalidOidcIntegrityWithoutRewritingSecret(bool missingSignature)
        {
            CottonEncryptionSettings currentSettings = ConfigurationBuilderExtensions
                .DeriveEncryptionSettings("database-field-protector-key-001");
            await using ServiceProvider legacyServices = CreateServiceProvider(
                CreateLegacySettings(currentSettings), includeRecovery: false, integritySettings: currentSettings);
            Guid providerId = await CreateProviderAsync(legacyServices, "legacy");
            await using CottonDbContext unsignedDbContext = new(
                new DbContextOptionsBuilder<CottonDbContext>()
                    .UseNpgsql(DbContext.Database.GetConnectionString()).Options,
                legacyServices.GetRequiredService<IDatabaseFieldProtector>());
            OidcProvider provider = await unsignedDbContext.OidcProviders.SingleAsync(x => x.Id == providerId);
            EntityEntry<OidcProvider> entry = unsignedDbContext.Entry(provider);
            if (missingSignature)
            {
                PropertyEntry<OidcProvider, byte[]?> signature = entry.Property<byte[]?>(DatabaseIntegrityColumns.MacProperty);
                signature.CurrentValue = null;
                signature.IsModified = true;
            }
            else
            {
                entry.Property(x => x.Name).CurrentValue = "Changed outside the application";
                entry.Property(x => x.Name).IsModified = true;
            }
            await unsignedDbContext.SaveChangesAsync();

            await using ServiceProvider currentServices = CreateServiceProvider(currentSettings);
            await using AsyncServiceScope scope = currentServices.CreateAsyncScope();
            if (missingSignature)
            {
                await Assert.ThatAsync(() => RepairAsync(currentServices, scope.ServiceProvider),
                    Throws.Exception.With.Message.StartsWith("Database integrity signature is missing"));
            }
            else
            {
                await Assert.ThrowsAsync<DatabaseIntegrityException>(() => RepairAsync(currentServices, scope.ServiceProvider));
            }

            await using AsyncServiceScope legacyScope = legacyServices.CreateAsyncScope();
            CottonDbContext legacyDbContext = legacyScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            OidcProvider unchanged = await legacyDbContext.OidcProviders.SingleAsync(x => x.Id == providerId);
            Assert.That(unchanged.ClientSecretEncrypted, Is.EqualTo("legacy-secret"));
        }

        [Test]
        public async Task RepairAsync_RejectsOidcSecretEncryptedWithUnrelatedKey()
        {
            CottonEncryptionSettings currentSettings = ConfigurationBuilderExtensions
                .DeriveEncryptionSettings("database-field-protector-key-001");
            CottonEncryptionSettings unrelatedSettings = ConfigurationBuilderExtensions
                .DeriveEncryptionSettings("database-field-protector-key-002");
            unrelatedSettings.MasterEncryptionKeyId = currentSettings.MasterEncryptionKeyId;
            await using ServiceProvider unrelatedServices = CreateServiceProvider(
                unrelatedSettings, includeRecovery: false, integritySettings: currentSettings);
            await CreateProviderAsync(unrelatedServices, "unrelated");

            await using ServiceProvider currentServices = CreateServiceProvider(currentSettings);
            await using AsyncServiceScope scope = currentServices.CreateAsyncScope();
            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => RepairAsync(currentServices, scope.ServiceProvider));
        }

        private static async Task<Guid> CreateProviderAsync(IServiceProvider services, string slug)
        {
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            OidcProvider provider = new()
            {
                Name = slug,
                Slug = slug,
                Issuer = "https://identity.example.com",
                ClientId = "cotton",
                ClientSecretEncrypted = $"{slug}-secret",
                Scopes = ["openid", "email"],
                IsEnabled = true,
                DefaultRole = UserRole.User,
            };
            dbContext.OidcProviders.Add(provider);
            await dbContext.SaveChangesAsync();
            return provider.Id;
        }

        private static async Task AssertProviderAsync(IServiceProvider services, Guid providerId, string slug)
        {
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            OidcProvider provider = await dbContext.OidcProviders.SingleAsync(x => x.Id == providerId);
            IDatabaseIntegrityVerifier integrity = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            integrity.RequireValid(dbContext, provider, "oidc.provider");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(provider.ClientSecretEncrypted, Is.EqualTo($"{slug}-secret"));
                Assert.That(provider.Name, Is.EqualTo(slug));
                Assert.That(provider.IsEnabled, Is.True);
                Assert.That(provider.Scopes, Is.EqualTo(new[] { "openid", "email" }));
            }
        }
    }
}
