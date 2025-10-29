using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting; // WebHostDefaults
using Microsoft.EntityFrameworkCore; // for EF Core
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Npgsql;
using Cotton.Server.Database;
using Cotton.Server.IntegrationTests.Abstractions;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests;

public class AuthSmokeTests : IntegrationTestBase
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = new();

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

        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", new { any = "thing" });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(payload!.token), Is.False, "Token must be present");

        // Basic JWT structure check: three dot-separated segments
        var parts = payload.token.Split('.');
        Assert.That(parts.Length, Is.EqualTo(3), "JWT must have 3 parts");

        // Verify the side-effect of login: default admin user is created
        var users = await DbContext.Users.AsNoTracking().ToListAsync();
        Assert.That(users.Count, Is.EqualTo(1));
        Assert.That(users[0].Username, Is.EqualTo("admin"));
    }

    [Test]
    public async Task Resolve_Root_Layout_Returns_RootNode()
    {
        Assert.That(_client, Is.Not.Null);

        // Login first to obtain JWT token
        var loginRes = await _client!.PostAsJsonAsync("/api/v1/auth/login", new { any = "thing" });
        loginRes.EnsureSuccessStatusCode();
        var login = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(login, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(login!.token), Is.False);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.token);

        var res = await _client.GetAsync("/api/v1/layouts/resolver");
        res.EnsureSuccessStatusCode();

        var node = await res.Content.ReadFromJsonAsync<LayoutNodeDto>();
        Assert.That(node, Is.Not.Null);
        Assert.That(node!.name, Is.EqualTo("/"));
        Assert.That(node.parentId, Is.Null);
        Assert.That(node.layoutId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(node.id, Is.Not.EqualTo(Guid.Empty));

        // Ensure DB reflects created layout + root node
        var layouts = await DbContext.UserLayouts.AsNoTracking().CountAsync();
        var nodes = await DbContext.UserLayoutNodes.AsNoTracking().CountAsync();
        Assert.That(layouts, Is.EqualTo(1));
        Assert.That(nodes, Is.EqualTo(1));
    }

    private sealed record LoginResponse(string token);
    private sealed record LayoutNodeDto(Guid id, Guid layoutId, Guid? parentId, string name);
}
