// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Server.Controllers;
using Cotton.Server.Handlers.Auth.Oidc;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Models;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public class OidcAuthorizationStartTests : IntegrationTestBase
    {
        public OidcAuthorizationStartTests() : base("cotton_oidc_correlation_tests")
        {
        }

        [SetUp]
        public Task SetUp() => RecreateDatabaseAsync();

        [Test]
        public async Task Start_CookieMatchesPersistedStateAndExpiry()
        {
            using DatabaseFieldProtector protector = new(
                ConfigurationBuilderExtensions.DeriveEncryptionSettings(Convert.ToHexString(RandomNumberGenerator.GetBytes(16))),
                NullLogger<DatabaseFieldProtector>.Instance);
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql(TestDataSource)
                .EnableServiceProviderCaching(false)
                .Options;
            await using CottonDbContext dbContext = new(options, protector);
            OidcProvider oidcProvider = new()
            {
                Name = "Test provider",
                Slug = "provider",
                Issuer = OidcDiscoveryTestTransport.Issuer,
                ClientId = "cotton",
                IsEnabled = true,
            };
            dbContext.OidcProviders.Add(oidcProvider);
            await dbContext.SaveChangesAsync();
            using HttpClient httpClient = new(new OidcDiscoveryTestTransport());
            SettingsProvider settings = new(dbContext, new ServerSettingsCache());
            BeginOidcAuthenticationRequestHandler handler = new(
                dbContext, new OidcDiscoveryService(httpClient), settings, new NoopDatabaseIntegrityVerifier());
            ServiceCollection services = new();
            services.AddLogging();
            services.AddMediator();
            services.AddSingleton<IRequestHandler<BeginOidcAuthenticationRequest, OidcAuthorizationResult>>(handler);
            await using ServiceProvider provider = services.BuildServiceProvider();
            DefaultHttpContext context = new();
            OidcController controller = new(provider.GetRequiredService<IMediator>())
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };

            ActionResult<OidcAuthorizationUrlDto> response = await controller.CreateSignInAuthorizationUrl(
                "provider", new() { ReturnUrl = "/files", TrustDevice = true }, CancellationToken.None);

            Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
            OidcAuthorizationUrlDto payload = (OidcAuthorizationUrlDto)((OkObjectResult)response.Result!).Value!;
            var query = QueryHelpers.ParseQuery(new Uri(payload.AuthorizationUrl).Query);
            OidcLoginState stored = await dbContext.OidcLoginStates.AsNoTracking().SingleAsync();
            SetCookieHeaderValue cookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single()!);
            Assert.Multiple(() =>
            {
                Assert.That(stored.StateHash, Is.EqualTo(OidcProtocol.HashOpaqueValue(query["state"].ToString())));
                Assert.That(cookie.Name.ToString(), Does.EndWith(stored.StateHash));
                Assert.That(cookie.Expires!.Value.UtcDateTime, Is.EqualTo(stored.ExpiresAt).Within(TimeSpan.FromSeconds(1)));
                Assert.That(stored.ReturnUrl, Is.EqualTo("/files"));
                Assert.That(stored.TrustDevice, Is.True);
                Assert.That(query["nonce"].ToString(), Is.EqualTo(stored.NonceEncrypted));
                Assert.That(query["code_challenge"].ToString(), Is.EqualTo(OidcProtocol.CreateCodeChallenge(stored.CodeVerifierEncrypted)));
                Assert.That(query["code_challenge_method"].ToString(), Is.EqualTo("S256"));
            });
        }
    }
}
