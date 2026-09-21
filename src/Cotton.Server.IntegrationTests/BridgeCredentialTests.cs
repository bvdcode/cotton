// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Bridge;
using Cotton.Server.Models.Bridge;
using Cotton.Server.Providers;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public class BridgeCredentialTests
    {
        [Test]
        public async Task ExistingCredential_IsReusedWithoutDatabaseOrNetworkAccess()
        {
            CottonServerSettings entity = new()
            {
                InstanceId = Guid.NewGuid(),
                TelemetryEnabled = true,
                CloudServicesTokenEncrypted = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            };
            ServerSettingsCache cache = new();
            cache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(entity));
            await using CottonDbContext database = CreateDatabase();
            GetBridgeCredentialRequestHandler handler = new(new SettingsProvider(database, cache), cache);
            BridgeCredential credential = await handler.Handle(new GetBridgeCredentialRequest(), CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(credential.InstanceId, Is.EqualTo(entity.InstanceId));
                Assert.That(credential.Token, Is.EqualTo(entity.CloudServicesTokenEncrypted));
            });
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void Registration_RequiresTelemetryConsentAndInitializedInstance(bool telemetry, bool initialized)
        {
            CottonServerSettings entity = new()
            {
                InstanceId = initialized ? Guid.NewGuid() : Guid.Empty,
                TelemetryEnabled = telemetry,
            };
            ServerSettingsCache cache = new();
            cache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(entity));
            using CottonDbContext database = CreateDatabase();
            GetBridgeCredentialRequestHandler handler = new(new SettingsProvider(database, cache), cache);
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await handler.Handle(new GetBridgeCredentialRequest(), CancellationToken.None));
        }

        private static CottonDbContext CreateDatabase()
        {
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused");
            return new CottonDbContext(options.Options);
        }
    }
}
