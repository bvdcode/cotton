using Cotton.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Cotton.Server.IntegrationTests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Cotton.Server.IntegrationTests.Common;

public class TestAppFactory(IReadOnlyDictionary<string, string?> overrides) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "IntegrationTests");
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(overrides);
        });
        builder.ConfigureServices(services =>
        {
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IStoragePipeline));
            if (existing != null) services.Remove(existing);
            services.AddSingleton<IStoragePipeline, InMemoryStorage>();
        });
        builder.ConfigureLogging((ctx, logging) =>
        {
            logging.ClearProviders();
            logging.AddProvider(new NUnitLoggerProvider());
            logging.SetMinimumLevel(LogLevel.Information);
        });
    }
}
