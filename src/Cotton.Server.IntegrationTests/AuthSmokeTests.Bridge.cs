// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Bridge;
using Cotton.Server.Models.Bridge;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class AuthSmokeTests
    {
        [Test]
        public async Task BridgeCredential_IsPersistedBeforeRegistrationAndReusedAcrossScopes()
        {
            await LoginAsync("testuser", "testpassword");
            await using (AsyncServiceScope setup = _customFactory!.Services.CreateAsyncScope())
            {
                SettingsProvider settings = setup.ServiceProvider.GetRequiredService<SettingsProvider>();
                await settings.SetPropertyAsync(x => x.TelemetryEnabled, true);
            }

            async Task<BridgeCredential> LoadCredential()
            {
                await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new GetBridgeCredentialRequest());
            }

            BridgeCredential[] credentials = await Task.WhenAll(
                Enumerable.Range(0, 4).Select(_ => LoadCredential()));
            Assert.That(credentials.Distinct().Count(), Is.EqualTo(1));
            Assert.That(credentials[0].Token, Has.Length.EqualTo(64));
            _customFactory!.Services.GetRequiredService<ServerSettingsCache>()
                .InvalidateSettings(serverIsInitialized: true);
            BridgeCredential restored = await LoadCredential();
            await using AsyncServiceScope readback = _customFactory.Services.CreateAsyncScope();
            CottonDbContext database = readback.ServiceProvider.GetRequiredService<CottonDbContext>();
            string? persisted = await database.ServerSettings
                .Select(x => x.CloudServicesTokenEncrypted).SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(restored, Is.EqualTo(credentials[0]));
                Assert.That(persisted, Is.EqualTo(restored.Token));
            });
        }
    }
}
