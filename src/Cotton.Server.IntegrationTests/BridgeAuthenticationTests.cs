// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Bridge;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Models.Bridge;
using Cotton.Server.Services.Bridge;
using EasyExtensions.Mediator;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class BridgeAuthenticationTests
    {
        [Test]
        public async Task ServiceRequests_IncludeBothTokenAndInstanceId()
        {
            BridgeTestCredentialProvider credentials = new();
            using BridgeTestTransport transport = new();
            using BridgeAuthenticationHandler handler = new(credentials) { InnerHandler = transport };
            using HttpClient client = new(handler);
            using HttpResponseMessage response = await client.GetAsync(global::Cotton.Constants.CottonBridgeGeoIpLookupUrl);
            Assert.That(transport.Requests.Single(), Is.EqualTo(
                (credentials.Credential.Token, credentials.Credential.InstanceId.ToString())));
        }

        [Test]
        public async Task HealthChecks_DoNotRegisterAnInstance()
        {
            BridgeTestCredentialProvider credentials = new();
            using BridgeTestTransport transport = new();
            using BridgeAuthenticationHandler handler = new(credentials) { InnerHandler = transport };
            using HttpClient client = new(handler);
            using HttpResponseMessage response = await client.GetAsync(global::Cotton.Constants.CottonBridgeHealthUrl);
            Assert.Multiple(() =>
            {
                Assert.That(credentials.Calls, Is.Zero);
                Assert.That(transport.Requests.Single().Token, Is.Null);
            });
        }

        [TestCase("https://example.test/api/v1/lookup")]
        [TestCase("http://bridge.cottoncloud.dev/api/v1/lookup")]
        [TestCase("https://bridge.cottoncloud.dev/unrelated")]
        public void Credentials_AreNeverSentToAnotherDestination(string url)
        {
            BridgeTestCredentialProvider credentials = new();
            using BridgeTestTransport transport = new();
            using BridgeAuthenticationHandler handler = new(credentials) { InnerHandler = transport };
            using HttpClient client = new(handler);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await client.GetAsync(url));
            Assert.That(transport.Requests, Is.Empty);
            Assert.That(credentials.Calls, Is.Zero);
        }

        [Test]
        public async Task ConcurrentFirstRequests_RegisterOnce()
        {
            using BridgeTestTransport transport = new();
            await using ServiceProvider services = CreateServices(transport);
            BridgeCredentialProvider provider = services.GetRequiredService<BridgeCredentialProvider>();
            BridgeCredential[] credentials = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(_ => provider.GetAsync(CancellationToken.None)));
            Assert.That(transport.Requests.Count, Is.EqualTo(1));
            Assert.That(credentials.Distinct().Count(), Is.EqualTo(1));
            Assert.That(transport.Requests[0].Token, Is.EqualTo(credentials[0].Token));
        }

        [Test]
        public async Task FailedRegistration_RetriesWithTheSamePersistedCredential()
        {
            using BridgeTestTransport transport = new() { Status = HttpStatusCode.ServiceUnavailable };
            await using ServiceProvider services = CreateServices(transport);
            BridgeCredentialProvider provider = services.GetRequiredService<BridgeCredentialProvider>();
            Assert.ThrowsAsync<HttpRequestException>(async () => await provider.GetAsync(CancellationToken.None));
            transport.Status = HttpStatusCode.NoContent;
            await provider.GetAsync(CancellationToken.None);
            Assert.That(transport.Requests.Count, Is.EqualTo(2));
            Assert.That(transport.Requests[1], Is.EqualTo(transport.Requests[0]));
        }

        private static ServiceProvider CreateServices(BridgeTestTransport transport)
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddMediator();
            services.AddSingleton<BridgeTestCredentialProvider>();
            services.AddScoped<IRequestHandler<GetBridgeCredentialRequest, BridgeCredential>, BridgeTestCredentialHandler>();
            services.AddCottonBridgeClients();
            services.AddSingleton<BridgeCredentialProvider>();
            services.AddHttpClient(BridgeCredentialProvider.RegistrationClientName)
                .ConfigurePrimaryHttpMessageHandler(() => transport);
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
    }
}
