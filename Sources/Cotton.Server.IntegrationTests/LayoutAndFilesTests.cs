using Npgsql;
using System.Net;
using System.Text;
using NUnit.Framework;
using System.Net.Http.Json;
using Cotton.Server.Models;
using Cotton.Server.Services;
using System.Net.Http.Headers;
using Cotton.Server.Models.Dto;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Models.Requests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Storage;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using EasyExtensions.Models;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;

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

        // Upload 10 unique chunks and create files from them
        for (int i = 1; i <= 10; i++)
        {
            var content = Encoding.UTF8.GetBytes($"hello {i}");
            var chunkHashLower = Convert.ToHexString(Hasher.HashData(content)).ToLowerInvariant();
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
                ChunkHashes = [chunkHashLower],
                Name = fileName,
                ContentType = "text/plain",
                Hash = chunkHashLower,
                NodeId = child.Id
            };
            var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createFileRes.EnsureSuccessStatusCode();
        }

        // Verify children listing shows 10 files
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
        var res = await _client!.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto()
        {
            Username = "testuser",
            Password = "testpassword"
        });
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        TestContext.Progress.WriteLine($"Login OK. Token: {login!.AccessToken[..Math.Min(16, login.AccessToken.Length)]}...");
        return login.AccessToken;
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
        // Second create with same name under same parent should return conflict (409)
        var r2 = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", req);
        Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        TestContext.Progress.WriteLine($"Duplicate create returned status: {(int)r2.StatusCode} {r2.StatusCode}");

        // Verify DB has only one such node
        var duplicates = await DbContext.Nodes
            .AsNoTracking()
            .Where(n => n.ParentId == root.Id && n.Name == name)
            .CountAsync();
        Assert.That(duplicates, Is.EqualTo(1));
    }
}
