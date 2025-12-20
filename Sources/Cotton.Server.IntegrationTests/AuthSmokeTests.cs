// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Storage.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using System.Net.Http.Json;

namespace Cotton.Server.IntegrationTests;

public class AuthSmokeTests : IntegrationTestBase
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        // Reset DB to empty state
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();
        Assert.Multiple(() =>
        {
            Assert.That(creator.Exists(), Is.True, "DB must exist after Create()");
            Assert.That(creator.HasTables(), Is.False, "DB must have no user tables after Create()");
        });

        // Build connection string (match IntegrationTestBase values)
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres"
        };

        _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "IntegrationTests");
            builder.ConfigureAppConfiguration((ctx, cfg) =>
 {
     var overrides = new Dictionary<string, string?>
     {
         ["DatabaseSettings:Host"] = csb.Host,
         ["DatabaseSettings:Port"] = csb.Port.ToString(),
         ["DatabaseSettings:Database"] = csb.Database,
         ["DatabaseSettings:Username"] = csb.Username,
         ["DatabaseSettings:Password"] = csb.Password,
         ["MasterEncryptionKey"] = "IntegrationTestsKey",
         ["MasterEncryptionKeyId"] = "1",
         ["EncryptionThreads"] = "1",
         ["MaxChunkSizeBytes"] = "16777216",
         ["CipherChunkSizeBytes"] = "20971520",
         ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
     };
     cfg.AddInMemoryCollection(overrides!);
 });
            builder.ConfigureServices(services =>
            {
                // Replace file storage with in-memory implementation for tests
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
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Login_Returns_Token()
    {
        Assert.That(_client, Is.Not.Null);

        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto()
        {
            Username = "testuser",
            Password = "testpassword"
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(payload!.AccessToken), Is.False, "Token must be present");

        // Basic JWT structure check: three dot-separated segments
        var parts = payload.AccessToken.Split('.');
        Assert.That(parts.Length, Is.EqualTo(3), "JWT must have3 parts");

        // Log a short token preview
        TestContext.Progress.WriteLine($"Login OK. Token: {payload.AccessToken[..Math.Min(16, payload.AccessToken.Length)]}...");
    }
}
