// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Cotton.Server.IntegrationTests.Common;

public class TestAppFactory(Dictionary<string, string?> _overrides) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(_overrides);
        });

        builder.UseEnvironment("Testing");
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var quartzHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                    (d.ImplementationType == typeof(QuartzHostedService) ||
                        d.ImplementationFactory?.Method.ReturnType == typeof(QuartzHostedService)))
                .ToList();
            foreach (var d in quartzHosted)
            {
                services.Remove(d);
            }
            CottonServerSettings serverSettings = new();
            services.AddSingleton(serverSettings);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure host is created per test without cross-test reuse
        builder.UseEnvironment("IntegrationTests");
        return base.CreateHost(builder);
    }
}
