// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Helpers;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class RequestBaseUrlHelpersTests
    {
        [Test]
        public void GetBaseUrl_UsesRequestSchemeWhenForwardedProtoIsMissing()
        {
            HttpRequest request = CreateRequest("http", "cotton.test");

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

            Assert.That(baseUrl, Is.EqualTo("http://cotton.test"));
        }

        [Test]
        public void GetBaseUrl_UsesForwardedProtoForUrlGeneration()
        {
            HttpRequest request = CreateRequest("http", "cotton.test");
            request.Headers["X-Forwarded-Proto"] = "https";

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

            Assert.Multiple(() =>
            {
                Assert.That(baseUrl, Is.EqualTo("https://cotton.test"));
                Assert.That(request.Scheme, Is.EqualTo("http"));
                Assert.That(request.HttpContext.Connection.RemoteIpAddress, Is.Null);
            });
        }

        [Test]
        public void GetBaseUrl_UsesFirstForwardedProtoValue()
        {
            HttpRequest request = CreateRequest("http", "cotton.test");
            request.Headers["X-Forwarded-Proto"] = "https, http";

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

            Assert.That(baseUrl, Is.EqualTo("https://cotton.test"));
        }

        [Test]
        public void GetBaseUrl_UsesForwardedProtoFromConfiguredProxy()
        {
            HttpRequest request = CreateRequest("http", "cotton.test", "192.0.2.10");
            request.Headers["X-Forwarded-Proto"] = "https";

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(
                request,
                IPAddress.Parse("192.0.2.10"));

            Assert.That(baseUrl, Is.EqualTo("https://cotton.test"));
        }

        [Test]
        public void GetBaseUrl_UsesForwardedProtoFromConfiguredProxyNetwork()
        {
            HttpRequest request = CreateRequest("http", "cotton.test", "172.21.0.1");
            request.Headers["X-Forwarded-Proto"] = "https";

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(
                request,
                IPAddress.Parse("172.16.0.0"),
                trustedProxyPrefixLength: 12);

            Assert.That(baseUrl, Is.EqualTo("https://cotton.test"));
        }

        [Test]
        public void GetBaseUrl_IgnoresForwardedProtoFromUntrustedConnection()
        {
            HttpRequest request = CreateRequest("http", "cotton.test", "192.0.2.11");
            request.Headers["X-Forwarded-Proto"] = "https";

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(
                request,
                IPAddress.Parse("192.0.2.10"));

            Assert.That(baseUrl, Is.EqualTo("http://cotton.test"));
        }

        [Test]
        public void GetBaseUrl_DirectModeIgnoresForwardedProto()
        {
            HttpRequest request = CreateRequest("http", "cotton.test", "198.51.100.25");
            request.Headers["X-Forwarded-Proto"] = "https";

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request, IPAddress.Any);

            Assert.That(baseUrl, Is.EqualTo("http://cotton.test"));
        }

        [Test]
        public void GetBaseUrl_IgnoresUnsupportedForwardedProto()
        {
            HttpRequest request = CreateRequest("http", "cotton.test");
            request.Headers["X-Forwarded-Proto"] = "ftp";

            string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

            Assert.That(baseUrl, Is.EqualTo("http://cotton.test"));
        }

        private static HttpRequest CreateRequest(
            string scheme,
            string host,
            string? remoteIpAddress = null)
        {
            DefaultHttpContext context = new();
            context.Request.Scheme = scheme;
            context.Request.Host = new HostString(host);
            if (remoteIpAddress is not null)
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
            }

            return context.Request;
        }
    }
}
