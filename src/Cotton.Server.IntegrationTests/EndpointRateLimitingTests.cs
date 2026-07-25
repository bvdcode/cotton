// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

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
}
