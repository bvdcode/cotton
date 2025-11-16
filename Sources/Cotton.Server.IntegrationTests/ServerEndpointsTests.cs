using Npgsql;
using NUnit.Framework;
using System.Net.Http.Json;
using Cotton.Server.Models;
using System.Net.Http.Headers;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.IntegrationTests;

public class ServerEndpointsTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();
        Assert.Multiple(() =>
        {
            Assert.That(creator.Exists(), Is.True);
            Assert.That(creator.HasTables(), Is.False);
        });

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres"
        };
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

        _factory = new TestAppFactory(overrides);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Get_Settings_Works()
    {
        var res = await _client!.GetAsync("/api/v1/settings");
        res.EnsureSuccessStatusCode();
        var settings = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.ContainsKey("maxChunkSizeBytes"), Is.True);
        Assert.That(settings!.ContainsKey("supportedHashAlgorithm"), Is.True);
    }

    [Test]
    public async Task Get_CurrentUser_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await _client.GetFromJsonAsync<Models.Dto.UserDto>("/api/v1/users/me");
        Assert.That(me, Is.Not.Null);
        Assert.That(me!.Username, Is.EqualTo("testuser"));
    }

    private async Task<string> LoginAsync()
    {
        var res = await _client!.PostAsJsonAsync("/api/v1/auth/login", new Models.Requests.LoginRequest("testuser", "testpassword"));
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
