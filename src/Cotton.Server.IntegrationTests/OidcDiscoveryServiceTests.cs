// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class OidcDiscoveryServiceTests
    {
        private const string Issuer = "https://identity.example.com/application/o/cotton";

        [Test]
        public void Discovery_ConnectionFailurePreservesOriginalCause()
        {
            HttpRequestException cause = new("Connection refused");
            using HttpClient httpClient = new(new FailingHttpMessageHandler(cause));
            OidcDiscoveryService discovery = new(httpClient);

            Exception? result = Assert.CatchAsync(() => discovery.GetConfigurationAsync(CreateProvider(), CancellationToken.None));

            Assert.That(result, Is.Not.InstanceOf<BadRequestException<OidcProvider>>());
            Assert.That(result!.GetBaseException(), Is.SameAs(cause));
        }

        [Test]
        public void Discovery_UnexpectedFailurePreservesOriginalCause()
        {
            InvalidOperationException cause = new("Invalid transport configuration");
            using HttpClient httpClient = new(new FailingHttpMessageHandler(cause));
            OidcDiscoveryService discovery = new(httpClient);

            Exception? result = Assert.CatchAsync(() => discovery.GetConfigurationAsync(CreateProvider(), CancellationToken.None));

            Assert.That(result!.GetBaseException(), Is.SameAs(cause));
        }

        [Test]
        public void ValidateConfiguration_WithCompleteDiscovery_Succeeds()
        {
            OidcProvider provider = CreateProvider();
            OpenIdConnectConfiguration configuration = CreateConfiguration();

            Assert.DoesNotThrow(() => OidcDiscoveryService.ValidateConfiguration(provider, configuration));
        }

        [Test]
        public void ValidateConfiguration_WithDifferentIssuer_RejectsDiscovery()
        {
            OidcProvider provider = CreateProvider();
            OpenIdConnectConfiguration configuration = CreateConfiguration();
            configuration.Issuer = "https://identity.example.com/application/o/other";

            BadRequestException<OidcProvider>? exception = Assert.Throws<BadRequestException<OidcProvider>>(
                () => OidcDiscoveryService.ValidateConfiguration(provider, configuration));

            Assert.That(exception!.Message, Does.Contain("issuer does not match"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("http://identity.example.com/authorize")]
        [TestCase("/authorize")]
        public void ValidateConfiguration_WithInvalidAuthorizationEndpoint_RejectsDiscovery(string? endpoint)
        {
            OidcProvider provider = CreateProvider();
            OpenIdConnectConfiguration configuration = CreateConfiguration();
            configuration.AuthorizationEndpoint = endpoint;

            BadRequestException<OidcProvider>? exception = Assert.Throws<BadRequestException<OidcProvider>>(
                () => OidcDiscoveryService.ValidateConfiguration(provider, configuration));

            Assert.That(exception!.Message, Does.Contain("authorization endpoint"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("http://identity.example.com/token")]
        [TestCase("/token")]
        public void ValidateConfiguration_WithInvalidTokenEndpoint_RejectsDiscovery(string? endpoint)
        {
            OidcProvider provider = CreateProvider();
            OpenIdConnectConfiguration configuration = CreateConfiguration();
            configuration.TokenEndpoint = endpoint;

            BadRequestException<OidcProvider>? exception = Assert.Throws<BadRequestException<OidcProvider>>(
                () => OidcDiscoveryService.ValidateConfiguration(provider, configuration));

            Assert.That(exception!.Message, Does.Contain("token endpoint"));
        }

        [Test]
        public void ValidateConfiguration_WithoutSigningKeys_RejectsDiscovery()
        {
            OidcProvider provider = CreateProvider();
            OpenIdConnectConfiguration configuration = CreateConfiguration();
            configuration.SigningKeys.Clear();

            BadRequestException<OidcProvider>? exception = Assert.Throws<BadRequestException<OidcProvider>>(
                () => OidcDiscoveryService.ValidateConfiguration(provider, configuration));

            Assert.That(exception!.Message, Does.Contain("signing keys"));
        }

        private static OidcProvider CreateProvider()
        {
            return new OidcProvider
            {
                Issuer = Issuer
            };
        }

        private static OpenIdConnectConfiguration CreateConfiguration()
        {
            OpenIdConnectConfiguration configuration = new()
            {
                Issuer = Issuer,
                AuthorizationEndpoint = $"{Issuer}/authorize",
                TokenEndpoint = $"{Issuer}/token"
            };
            configuration.SigningKeys.Add(new SymmetricSecurityKey(new byte[32]));
            return configuration;
        }
    }
}
