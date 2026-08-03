// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Auth;
using Cotton.Server.Controllers;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;
using System.Reflection;
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

    [Test]
    public void PublicShareArchive_UsesArchiveRateLimit()
    {
        MethodInfo? archiveAction = typeof(LayoutController)
            .GetMethod(nameof(LayoutController.CreateSharedArchiveDownloadLink));
        EnableRateLimitingAttribute? attribute = archiveAction?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.That(attribute?.PolicyName, Is.EqualTo(AuthRateLimitPolicies.PublicShareArchive));
    }

    private static HttpRequest CreateRequest(string forwardedAddress)
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["X-Forwarded-For"] = forwardedAddress;
        return context.Request;
    }
}
