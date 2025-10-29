using Cotton.Server.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cotton.Server.IntegrationTests.Common;

public class TestAppFactory : WebApplicationFactory<Program>
{
 private readonly IReadOnlyDictionary<string, string?> _overrides;
 public TestAppFactory(IReadOnlyDictionary<string, string?> overrides)
 {
 _overrides = overrides;
 }

 protected override void ConfigureWebHost(IWebHostBuilder builder)
 {
 builder.UseSetting(WebHostDefaults.EnvironmentKey, "IntegrationTests");
 builder.ConfigureAppConfiguration((ctx, cfg) =>
 {
 cfg.AddInMemoryCollection(_overrides);
 });
 builder.ConfigureServices(services =>
 {
 var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IStorage));
 if (existing != null) services.Remove(existing);
 services.AddSingleton<IStorage, InMemoryStorage>();
 });
 builder.ConfigureLogging((ctx, logging) =>
 {
 logging.ClearProviders();
 logging.AddProvider(new NUnitLoggerProvider());
 logging.SetMinimumLevel(LogLevel.Information);
 });
 }
}
