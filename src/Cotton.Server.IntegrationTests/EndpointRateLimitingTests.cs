// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Auth;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;
using System.Threading.RateLimiting;

namespace Cotton.Server.IntegrationTests;

public class EndpointRateLimitingTests
{
    [Test]
    public void AddEndpointRateLimiting_DoesNotInstallGlobalLimiter()
    {
        ServiceCollection services = new();
        services.AddEndpointRateLimiting();

        using ServiceProvider provider = services.BuildServiceProvider();
        RateLimiterOptions options = provider
            .GetRequiredService<IOptions<RateLimiterOptions>>()
            .Value;

        Assert.That(options.GlobalLimiter, Is.Null);
    }

    [Test]
    public void RemoteAddressPartition_UsesForwardedClientAddress()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";

        string partition = context.Request
            .GetTrustedClientIPAddress(trustedProxyIpAddress: null)
            .ToString();

        Assert.That(partition, Is.EqualTo("203.0.113.42"));
    }

    [Test]
    public void TrustedClientAddress_PrefersCloudflareHeader()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.40";
        context.Request.Headers["X-Real-IP"] = "203.0.113.41";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";

        IPAddress address = context.Request.GetTrustedClientIPAddress(trustedProxyIpAddress: null);

        Assert.That(address, Is.EqualTo(IPAddress.Parse("203.0.113.40")));
    }

    [Test]
    public void TrustedClientAddress_AcceptsHeadersFromConfiguredProxy()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.40";

        IPAddress address = context.Request.GetTrustedClientIPAddress(
            IPAddress.Parse("192.0.2.10"));

        Assert.That(address, Is.EqualTo(IPAddress.Parse("203.0.113.40")));
    }

    [Test]
    public void TrustedClientAddress_AcceptsPrivate172ProxyAfterDockerNetworkChanges()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.16.0.1");
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.40";

        IPAddress address = context.Request.GetTrustedClientIPAddress(
            IPAddress.Parse("172.16.0.0"),
            trustedProxyPrefixLength: 12);

        Assert.That(address, Is.EqualTo(IPAddress.Parse("203.0.113.40")));
    }

    [TestCase("172.21.0.1", "172.16.0.0/12")]
    [TestCase("127.0.0.1", "127.0.0.1/32")]
    [TestCase("::1", "::1/128")]
    public void TrustedProxyConfiguration_SuggestsExpectedNetwork(
        string observedAddress,
        string expectedConfiguration)
    {
        string suggested = TrustedProxyRequestExtensions.GetSuggestedProxyConfiguration(
            IPAddress.Parse(observedAddress));

        Assert.That(suggested, Is.EqualTo(expectedConfiguration));
    }

    [Test]
    public void TrustedProxyConfiguration_KeepsPrivate172AddressExactWithoutPrefix()
    {
        bool parsed = TrustedProxyRequestExtensions.TryParseTrustedProxy(
            "172.21.0.1",
            out IPAddress address,
            out byte? prefixLength);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(address, Is.EqualTo(IPAddress.Parse("172.21.0.1")));
            Assert.That(prefixLength, Is.Null);
            Assert.That(
                TrustedProxyRequestExtensions.MatchesTrustedProxy(
                    address,
                    prefixLength,
                    IPAddress.Parse("172.16.0.1")),
                Is.False);
        });
    }

    [TestCase("172.16.0.0/12", "172.16.0.0", 12)]
    [TestCase("172.21.0.1/12", "172.16.0.0", 12)]
    [TestCase("172.21.0.0/16", "172.21.0.0", 16)]
    [TestCase("192.0.2.0/24", "192.0.2.0", 24)]
    [TestCase("2001:db8::/64", "2001:db8::", 64)]
    public void TrustedProxyConfiguration_ParsesCidrNetwork(
        string value,
        string expectedAddress,
        byte expectedPrefixLength)
    {
        bool parsed = TrustedProxyRequestExtensions.TryParseTrustedProxy(
            value,
            out IPAddress address,
            out byte? prefixLength);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(address, Is.EqualTo(IPAddress.Parse(expectedAddress)));
            Assert.That(prefixLength, Is.EqualTo(expectedPrefixLength));
            Assert.That(
                TrustedProxyRequestExtensions.FormatConfiguredProxy(address, prefixLength),
                Is.EqualTo($"{expectedAddress}/{expectedPrefixLength}"));
        });
    }

    [TestCase("192.0.2.1/33")]
    [TestCase("2001:db8::1/129")]
    [TestCase("not-an-address/12")]
    public void TrustedProxyConfiguration_RejectsInvalidNetwork(string value)
    {
        bool parsed = TrustedProxyRequestExtensions.TryParseTrustedProxy(
            value,
            out IPAddress _,
            out byte? _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void TrustedProxyConfiguration_KeepsPublic172AddressExact()
    {
        bool parsed = TrustedProxyRequestExtensions.TryParseTrustedProxy(
            "172.64.0.1",
            out IPAddress address,
            out byte? prefixLength);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(address, Is.EqualTo(IPAddress.Parse("172.64.0.1")));
            Assert.That(prefixLength, Is.Null);
            Assert.That(
                TrustedProxyRequestExtensions.FormatConfiguredProxy(address),
                Is.EqualTo("172.64.0.1"));
        });
    }

    [Test]
    public void TrustedClientAddress_DirectModeIgnoresForwardedHeaders()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.25");
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.40";
        context.Request.Headers["X-Real-IP"] = "203.0.113.41";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";

        IPAddress address = context.Request.GetTrustedClientIPAddress(
            TrustedProxyRequestExtensions.DirectConnectionIpAddress);

        Assert.That(address, Is.EqualTo(IPAddress.Parse("198.51.100.25")));
    }

    [Test]
    public void TrustedClientAddress_RejectsHeadersFromUntrustedConnection()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.11");
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.40";

        UntrustedProxyConnectionException exception = Assert.Throws<UntrustedProxyConnectionException>(() =>
            context.Request.GetTrustedClientIPAddress(IPAddress.Parse("192.0.2.10")))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.TrustedProxyIpAddress, Is.EqualTo(IPAddress.Parse("192.0.2.10")));
            Assert.That(exception.TrustedProxyPrefixLength, Is.Null);
            Assert.That(exception.ConnectingIpAddress, Is.EqualTo(IPAddress.Parse("192.0.2.11")));
        });
    }

    [Test]
    public void TrustedClientAddress_NormalizesIpv4MappedProxyAddress()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.0.2.10");
        context.Request.Headers["X-Real-IP"] = "203.0.113.41";

        IPAddress address = context.Request.GetTrustedClientIPAddress(
            IPAddress.Parse("192.0.2.10"));

        Assert.That(address, Is.EqualTo(IPAddress.Parse("203.0.113.41")));
    }

    [Test]
    public void ProxyServiceDetection_DoesNotGuessLocalProxyProduct()
    {
        DefaultHttpContext context = new();
        context.Request.Headers["CF-Ray"] = "230b030023ae2822-SJC";
        context.Request.Headers["CF-IPCountry"] = "US";
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.40";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.40";
        context.Request.Headers["X-Real-IP"] = "203.0.113.40";
        context.Request.Headers["X-Forwarded-Host"] = "cotton.example";
        context.Request.Headers["X-Forwarded-Port"] = "443";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-Server"] = "traefik-1";

        IReadOnlyList<string> services = context.Request.DetectProxyServices();
        CloudflareProxyMetadataDto? cloudflare = context.Request.DetectCloudflareMetadata();

        Assert.Multiple(() =>
        {
            Assert.That(services, Is.EqualTo(new[] { "cloudflare", "reverse-proxy" }));
            Assert.That(cloudflare?.VisitorCountryCode, Is.EqualTo("US"));
            Assert.That(cloudflare?.DatacenterCode, Is.EqualTo("SJC"));
        });
    }

    [Test]
    public void ProxyServiceDetection_ReportsGenericProxyForUnknownForwarder()
    {
        DefaultHttpContext context = new();
        context.Request.Headers["Forwarded"] = "for=203.0.113.40;proto=https";

        IReadOnlyList<string> services = context.Request.DetectProxyServices();

        Assert.That(services, Is.EqualTo(new[] { "reverse-proxy" }));
    }

    [TestCase("X-Amz-Cf-Id", "cloudfront")]
    [TestCase("X-Azure-FDID", "azure-front-door")]
    [TestCase("Fastly-Client-IP", "fastly")]
    [TestCase("Fly-Client-IP", "fly-io")]
    [TestCase("X-Vercel-Id", "vercel")]
    [TestCase("X-Amzn-Trace-Id", "aws-alb")]
    [TestCase("X-Envoy-External-Address", "envoy")]
    public void ProxyServiceDetection_RecognizesDistinctiveServiceHeaders(
        string headerName,
        string expectedService)
    {
        DefaultHttpContext context = new();
        context.Request.Headers[headerName] = "present";

        IReadOnlyList<string> services = context.Request.DetectProxyServices();

        Assert.That(services, Is.EqualTo(new[] { expectedService }));
    }

    [Test]
    public void ProxyServiceDetection_ReadsServerResponseAndPositionsLocalProbe()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Server.ParseAdd("nginx/1.27.4");

        IReadOnlyList<string> probed = ProxyServiceDetectionExtensions.DetectProxyServices(response);
        IReadOnlyList<string> merged = ProxyServiceDetectionExtensions.MergeProxyServices(
            ["cloudflare", "reverse-proxy"],
            probed);
        IReadOnlyList<string> edgeMerged = ProxyServiceDetectionExtensions.MergeProxyServices(
            ["reverse-proxy"],
            ["cloudflare"]);

        Assert.Multiple(() =>
        {
            Assert.That(probed, Is.EqualTo(new[] { "nginx" }));
            Assert.That(merged, Is.EqualTo(new[] { "cloudflare", "nginx" }));
            Assert.That(edgeMerged, Is.EqualTo(new[] { "cloudflare", "reverse-proxy" }));
        });
    }

    [Test]
    public async Task ProxyTopologyProbe_UsesHeadRequestAndReadsResponseServer()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Server.ParseAdd("Caddy");
        response.Headers.TryAddWithoutValidation("CF-Ray", "a2591eb86ff8cbaa-LAX");
        using var handler = new StaticResponseHandler(response);
        using var client = new HttpClient(handler);
        var probe = new ProxyTopologyProbeService(
            client,
            NullLogger<ProxyTopologyProbeService>.Instance);

        ProxyTopologyProbeResult result = await probe.DetectAsync("https://cotton.example/");

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestMethod, Is.EqualTo(HttpMethod.Head));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri("https://cotton.example/")));
            Assert.That(result.Services, Is.EqualTo(new[] { "cloudflare", "caddy" }));
            Assert.That(result.Cloudflare?.VisitorCountryCode, Is.Null);
            Assert.That(result.Cloudflare?.DatacenterCode, Is.EqualTo("LAX"));
        });
    }

    [Test]
    public void PublicShareLookupFailureLimiter_IsPartitionedByForwardedClientAddress()
    {
        using PublicShareLookupFailureLimiter limiter = new(request => request
            .GetTrustedClientIPAddress(trustedProxyIpAddress: null)
            .ToString());
        HttpRequest firstClient = CreateRequest("203.0.113.42");
        HttpRequest secondClient = CreateRequest("203.0.113.43");

        for (int i = 0; i < 60; i++)
        {
            using RateLimitLease lease = limiter.AttemptAcquire(firstClient);
            Assert.That(lease.IsAcquired, Is.True);
        }

        using RateLimitLease rejectedLease = limiter.AttemptAcquire(firstClient);
        using RateLimitLease separateClientLease = limiter.AttemptAcquire(secondClient);
        Assert.Multiple(() =>
        {
            Assert.That(rejectedLease.IsAcquired, Is.False);
            Assert.That(separateClientLease.IsAcquired, Is.True);
        });
    }

    private static HttpRequest CreateRequest(string forwardedAddress)
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["X-Forwarded-For"] = forwardedAddress;
        return context.Request;
    }

    private sealed class StaticResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpMethod? RequestMethod { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            return Task.FromResult(response);
        }
    }
}
