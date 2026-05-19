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

public class TestAppFactory : WebApplicationFactory<Program>
{
    private const string TestRootMasterKey = "testtesttesttesttesttesttesttest";
    private readonly Dictionary<string, string?> _overrides;
    private readonly string? _previousMasterKey;

    public TestAppFactory(Dictionary<string, string?> overrides)
    {
        _overrides = overrides;
        _previousMasterKey = Environment.GetEnvironmentVariable("COTTON_MASTER_KEY");
        Environment.SetEnvironmentVariable("COTTON_MASTER_KEY", TestRootMasterKey);
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("COTTON_MASTER_KEY", _previousMasterKey);
        base.Dispose(disposing);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Quartz.Logging.LogProvider.IsDisabled = true;

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

            var schedulerFactoryDescriptors = services
                .Where(d => d.ServiceType == typeof(ISchedulerFactory))
                .ToList();
            foreach (var d in schedulerFactoryDescriptors)
            {
                services.Remove(d);
            }
            services.AddSingleton<ISchedulerFactory, NoOpSchedulerFactory>();

            services.AddSingleton(new CottonServerSettings
            {
                MaxChunkSizeBytes = 128 * 1024 * 1024,
                CipherChunkSizeBytes = 20 * 1024 * 1024,
                EncryptionThreads = 1,
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure host is created per test without cross-test reuse
        builder.UseEnvironment("IntegrationTests");
        return base.CreateHost(builder);
    }
}
