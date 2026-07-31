// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Auth;
using Cotton.Server.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Reflection;

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

    [TestCase(typeof(FileController), nameof(FileController.Share))]
    [TestCase(typeof(LayoutController), nameof(LayoutController.GetSharedNodeInfo))]
    [TestCase(typeof(LayoutController), nameof(LayoutController.GetSharedNodeChildren))]
    [TestCase(typeof(LayoutController), nameof(LayoutController.GetSharedNodeAncestors))]
    [TestCase(typeof(LayoutController), nameof(LayoutController.DownloadSharedNodeFile))]
    public void PublicShareLookup_UsesLookupRateLimit(Type controllerType, string actionName)
    {
        MethodInfo? shareAction = controllerType.GetMethod(actionName);
        EnableRateLimitingAttribute? attribute = shareAction?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.That(attribute?.PolicyName, Is.EqualTo(AuthRateLimitPolicies.PublicShareLookup));
    }

    [Test]
    public void PublicShareArchive_UsesArchiveRateLimit()
    {
        MethodInfo? archiveAction = typeof(LayoutController)
            .GetMethod(nameof(LayoutController.CreateSharedArchiveDownloadLink));
        EnableRateLimitingAttribute? attribute = archiveAction?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.That(attribute?.PolicyName, Is.EqualTo(AuthRateLimitPolicies.PublicShareArchive));
    }
}
