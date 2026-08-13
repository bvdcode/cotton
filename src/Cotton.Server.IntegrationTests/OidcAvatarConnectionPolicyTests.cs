// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class OidcAvatarConnectionPolicyTests
    {
        [Test]
        public void CreateHandler_DisablesConnectionReuse()
        {
            using SocketsHttpHandler handler = OidcAvatarConnectionPolicy.CreateHandler();

            Assert.Multiple(() =>
            {
                Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.Zero));
                Assert.That(handler.PooledConnectionLifetime, Is.EqualTo(TimeSpan.Zero));
            });
        }

        [Test]
        public void SelectAllowedAddresses_FiltersNonPublicAddresses()
        {
            DnsEndPoint destination = new("avatars.example.com", 443);
            IPAddress publicAddress = IPAddress.Parse("203.0.114.10");
            IPAddress privateAddress = IPAddress.Parse("192.168.1.10");

            IPAddress[] allowed = OidcAvatarConnectionPolicy.SelectAllowedAddresses(
                [publicAddress, privateAddress],
                destination,
                trustedPrivateEndpoint: null);

            Assert.That(allowed, Is.EqualTo(new[] { publicAddress }));
        }

        [Test]
        public void SelectAllowedAddresses_AllowsConfiguredIssuerEndpoint()
        {
            DnsEndPoint destination = new("identity.home.arpa", 443);
            IPAddress privateAddress = IPAddress.Parse("192.168.1.10");

            IPAddress[] allowed = OidcAvatarConnectionPolicy.SelectAllowedAddresses(
                [privateAddress],
                destination,
                new DnsEndPoint("identity.home.arpa", 443));

            Assert.That(allowed, Is.EqualTo(new[] { privateAddress }));
        }

        [TestCase("other.home.arpa", 443)]
        [TestCase("identity.home.arpa", 8443)]
        public void SelectAllowedAddresses_DoesNotTrustAnotherOrigin(string host, int port)
        {
            IPAddress privateAddress = IPAddress.Parse("192.168.1.10");

            IPAddress[] allowed = OidcAvatarConnectionPolicy.SelectAllowedAddresses(
                [privateAddress],
                new DnsEndPoint(host, port),
                new DnsEndPoint("identity.home.arpa", 443));

            Assert.That(allowed, Is.Empty);
        }
    }
}
