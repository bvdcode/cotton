using Npgsql;
using NUnit.Framework;
using System.Net.Http.Json;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Abstractions;
using EasyExtensions.Models;

namespace Cotton.Server.IntegrationTests;

public class LayoutEndpointsTests : IntegrationTestBase
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
    public async Task Resolve_And_Create_Node_Then_List_Ancestors_Children_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        // create child
        var createNodeRes = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", new Cotton.Server.Models.Requests.CreateNodeRequest { ParentId = root!.Id, Name = "child" });
        createNodeRes.EnsureSuccessStatusCode();
        var child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
        Assert.That(child, Is.Not.Null);

        // resolve path
        var resolved = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver/child");
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Id, Is.EqualTo(child!.Id));

        // list ancestors
        var ancestors = await _client.GetFromJsonAsync<IEnumerable<NodeDto>>($"/api/v1/layouts/nodes/{child!.Id}/ancestors");
        Assert.That(ancestors, Is.Not.Null);
        Assert.That(ancestors!.Any(a => a.Id == root!.Id), Is.True);

        // list children for root
        var children = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
        Assert.That(children, Is.Not.Null);
        Assert.That(children!.Nodes.Any(n => n.Id == child!.Id), Is.True);
    }

    private async Task<string> LoginAsync()
    {
        var res = await _client!.PostAsJsonAsync("/api/v1/auth/login", new UsernameLoginRequest()
        {
            Username = "testuser", Password = "testpassword"
        });
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
