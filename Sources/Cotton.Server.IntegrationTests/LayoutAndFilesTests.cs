using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using Npgsql;
using Cotton.Server.Database;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Database.Models.Enums;

namespace Cotton.Server.IntegrationTests;

public class LayoutAndFilesTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
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

        // Build connection overrides
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
    public async Task Resolve_Root_Layout_Returns_RootNode()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var node = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(node, Is.Not.Null);
        Assert.That(node!.Name, Is.EqualTo(NodeType.Default.ToString()));
        Assert.That(node.ParentId, Is.Null);
        Assert.That(node.LayoutId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(node.Id, Is.Not.EqualTo(Guid.Empty));
        TestContext.Progress.WriteLine($"Resolved root layout. LayoutId={node.LayoutId}, RootId={node.Id}");
    }

    [Test]
    public async Task Create_Node_And_10_Files()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        // Create a new child node under root
        var nodeName = "test";
        var createNodeReq = new CreateNodeRequest { ParentId = root!.Id, Name = nodeName };
        var createNodeRes = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", createNodeReq);
        createNodeRes.EnsureSuccessStatusCode();
        var child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
        Assert.That(child, Is.Not.Null);
        TestContext.Progress.WriteLine($"Created node '{nodeName}' with Id={child!.Id}");

        // Upload10 unique chunks and create files from them
        for (int i = 1; i <= 10; i++)
        {
            var content = Encoding.UTF8.GetBytes($"hello {i}");
            var chunkHashLower = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            // Upload chunk
            using var form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    },
                    "file",
                    $"chunk{i}.bin"
                },
                { new StringContent(chunkHashLower), "hash" }
            };
            var upRes = await _client.PostAsync("/api/v1/chunks", form);
            upRes.EnsureSuccessStatusCode();
            TestContext.Progress.WriteLine($"Uploaded chunk {i}: {chunkHashLower[..16]}...");

            // Create file (server validates and maps hex → byte[] itself)
            var fileName = $"file{i}.txt";
            var fileReq = new CreateFileRequest
            {
                ChunkHashes = new[] { chunkHashLower },
                Name = fileName,
                ContentType = "text/plain",
                Sha256 = chunkHashLower,
                NodeId = child.Id
            };
            var createFileRes = await _client.PostAsJsonAsync("/api/v1/files", fileReq);
            createFileRes.EnsureSuccessStatusCode();
        }

        // Verify children listing shows10 files
        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{child!.Id}/children");
        Assert.That(list, Is.Not.Null);
        Assert.That(list!.Files.Count, Is.EqualTo(10));
        var names = list.Files
            .OrderBy(x => x.CreatedAt)
            .Select(f => f.Name)
            .ToArray();
        for (int i = 1; i <= 10; i++)
        {
            Assert.That(names[i - 1], Is.EqualTo($"file{i}.txt"));
        }
    }

    private async Task<string> LoginAsync()
    {
        var res = await _client!.PostAsJsonAsync("/api/v1/auth/login", new { any = "thing" });
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(login, Is.Not.Null);
        TestContext.Progress.WriteLine($"Login OK. Token: {login!.token[..Math.Min(16, login.token.Length)]}...");
        return login.token;
    }

    [Test]
    public async Task Cannot_Create_Duplicate_Node_Name_Within_Same_Parent()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var name = "dup";
        var req = new CreateNodeRequest { ParentId = root!.Id, Name = name };
        // First create should succeed
        var r1 = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", req);
        r1.EnsureSuccessStatusCode();
        // Second create with same name under same parent should throw due to unique index
        var ex = Assert.ThrowsAsync<DbUpdateException>(async () =>
            await _client.PutAsJsonAsync("/api/v1/layouts/nodes", req));
        Assert.That(ex, Is.Not.Null);
        if (ex!.InnerException is PostgresException pg)
        {
            Assert.That(pg.SqlState, Is.EqualTo("23505")); // unique_violation
        }
        TestContext.Progress.WriteLine($"Duplicate create threw: {ex!.GetType().Name} -> {ex.InnerException?.GetType().Name}");

        // Verify DB has only one such node
        var duplicates = await DbContext.Nodes
            .AsNoTracking()
            .Where(n => n.ParentId == root.Id && n.Name == name)
            .CountAsync();
        Assert.That(duplicates, Is.EqualTo(1));
    }

    private sealed record LoginResponse(string token);
}
