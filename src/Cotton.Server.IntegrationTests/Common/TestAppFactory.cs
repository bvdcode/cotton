// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Quartz;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.IntegrationTests.Common
{
    public class TestAppFactory : WebApplicationFactory<Program>
    {
        public const string RemoteIpAddressHeader = "X-Cotton-Test-Remote-IP";

        private const string TestRootMasterKey = "testtesttesttesttesttesttesttest";
        private static readonly string StorageRoot = Path.Combine(
            Path.GetTempPath(),
            "cotton-server-integration-storage",
            Guid.NewGuid().ToString("N"));
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
            string storagePath = GetStoragePath();

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(_overrides);
            });

            builder.UseEnvironment("Testing");
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                List<ServiceDescriptor> quartzHosted = services
                    .Where(d => d.ServiceType == typeof(IHostedService) &&
                        (d.ImplementationType == typeof(QuartzHostedService) ||
                            d.ImplementationFactory?.Method.ReturnType == typeof(QuartzHostedService)))
                    .ToList();
                foreach (ServiceDescriptor? d in quartzHosted)
                {
                    services.Remove(d);
                }

                services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>();
                services.AddSingleton<IProxyTopologyProbeService, NoOpProxyTopologyProbeService>();

                ServiceDescriptor[] storageBackendProviders = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IStorageBackendProvider))
                    .ToArray();
                foreach (ServiceDescriptor descriptor in storageBackendProviders)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IStorageBackendProvider>(serviceProvider =>
                    new StaticStorageBackendProvider(
                        ActivatorUtilities.CreateInstance<FileSystemStorageBackend>(
                            serviceProvider,
                            storagePath)));

                services.AddSingleton(new CottonServerSettings
                {
                    MaxChunkSizeBytes = 128 * 1024 * 1024,
                    CipherChunkSizeBytes = 20 * 1024 * 1024,
                    EncryptionThreads = 1,
                });
            });
        }

        internal static void DeleteStorage()
        {
            TestDirectory.Delete(StorageRoot);
        }

        private string GetStoragePath()
        {
            string database = _overrides.GetValueOrDefault("DatabaseSettings:Database") ?? "default";
            string masterKey = _overrides.GetValueOrDefault("MasterEncryptionKey") ?? TestRootMasterKey;
            byte[] identity = Encoding.UTF8.GetBytes($"{database}\n{masterKey}");
            string storageId = Convert.ToHexStringLower(SHA256.HashData(identity));
            return Path.Combine(StorageRoot, storageId);
        }

        private class TestRemoteIpStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.Use(async (context, nextMiddleware) =>
                    {
                        if (context.Request.Headers.TryGetValue(RemoteIpAddressHeader, out StringValues values)
                            && IPAddress.TryParse(values.ToString(), out IPAddress? remoteIpAddress))
                        {
                            context.Connection.RemoteIpAddress = remoteIpAddress;
                        }

                        await nextMiddleware();
                    });
                    next(app);
                };
            }
        }

        private class NoOpProxyTopologyProbeService : IProxyTopologyProbeService
        {
            public Task<ProxyTopologyProbeResult> DetectAsync(
                string publicBaseUrl,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ProxyTopologyProbeResult([], null));
            }
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Ensure host is created per test without cross-test reuse
            builder.UseEnvironment("IntegrationTests");
            IHost host = base.CreateHost(builder);
            Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpQuartzLogProvider());
            return host;
        }
    }
}
