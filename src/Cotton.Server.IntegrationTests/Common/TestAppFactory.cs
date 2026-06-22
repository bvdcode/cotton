// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
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
    private readonly Dictionary<string, string?> _previousEnvironmentVariables = [];

    public TestAppFactory(Dictionary<string, string?> overrides)
    {
        _overrides = overrides;
        SetEnvironmentVariable(ConfigurationBuilderExtensions.MasterKeyEnvironmentVariable, TestRootMasterKey);
        SetDatabaseEnvironmentVariable("COTTON_PG_HOST", "DatabaseSettings:Host");
        SetDatabaseEnvironmentVariable("COTTON_PG_PORT", "DatabaseSettings:Port");
        SetDatabaseEnvironmentVariable("COTTON_PG_DATABASE", "DatabaseSettings:Database");
        SetDatabaseEnvironmentVariable("COTTON_PG_USERNAME", "DatabaseSettings:Username");
        SetDatabaseEnvironmentVariable("COTTON_PG_PASSWORD", "DatabaseSettings:Password");
    }

    protected override void Dispose(bool disposing)
    {
        foreach ((string key, string? value) in _previousEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        base.Dispose(disposing);
    }

    private void SetDatabaseEnvironmentVariable(string environmentVariable, string overrideKey)
    {
        if (_overrides.TryGetValue(overrideKey, out string? value))
        {
            SetEnvironmentVariable(environmentVariable, value);
        }
    }

    private void SetEnvironmentVariable(string key, string? value)
    {
        _previousEnvironmentVariables.TryAdd(key, Environment.GetEnvironmentVariable(key));
        Environment.SetEnvironmentVariable(key, value);
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
            foreach (ServiceDescriptor? d in quartzHosted)
            {
                services.Remove(d);
            }

            var schedulerFactoryDescriptors = services
                .Where(d => d.ServiceType == typeof(ISchedulerFactory))
                .ToList();
            foreach (ServiceDescriptor? d in schedulerFactoryDescriptors)
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
