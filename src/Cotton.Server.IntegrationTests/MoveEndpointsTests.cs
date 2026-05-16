// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class MoveEndpointsTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _client;
    private Dictionary<string, string?> _overrides = new();

    [SetUp]
    public void SetUp()
    {
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres"
        };
        _overrides = new Dictionary<string, string?>
        {
            ["DatabaseSettings:Host"] = csb.Host,
            ["DatabaseSettings:Port"] = csb.Port.ToString(),
            ["DatabaseSettings:Database"] = csb.Database,
            ["DatabaseSettings:Username"] = csb.Username,
            ["DatabaseSettings:Password"] = csb.Password,
            ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
            ["MasterEncryptionKeyId"] = "1",
            ["EncryptionThreads"] = "1",
            ["MaxChunkSizeBytes"] = "16777216",
            ["CipherChunkSizeBytes"] = "20971520",
            ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
        };

        _factory = new TestAppFactory(_overrides);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    // ---------------------------------------------------------------------
    // MoveFile
    // ---------------------------------------------------------------------

    [Test]
    public async Task MoveFile_ToAnotherFolder_Succeeds()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var dst = await CreateFolderAsync(root.Id, "dst");
        var file = await CreateFileAsync(src.Id, "doc.txt", "hello-1");

        var res = await MoveFileAsync(file.Id, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var children = await GetChildrenAsync(dst.Id);
        Assert.That(children.Files.Any(f => f.Id == file.Id), Is.True, "moved file must appear in destination");

        var srcChildren = await GetChildrenAsync(src.Id);
        Assert.That(srcChildren.Files.Any(f => f.Id == file.Id), Is.False, "moved file must not remain in source");
    }

    [Test]
    public async Task MoveFile_SameParent_IsNoOp()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "folder");
        var file = await CreateFileAsync(folder.Id, "doc.txt", "hello-2");

        var res = await MoveFileAsync(file.Id, folder.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var children = await GetChildrenAsync(folder.Id);
        Assert.That(children.Files.Count(f => f.Id == file.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task MoveFile_NameCollisionWithSiblingFile_Returns409()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var dst = await CreateFolderAsync(root.Id, "dst");
        var moving = await CreateFileAsync(src.Id, "doc.txt", "moving-content");
        await CreateFileAsync(dst.Id, "doc.txt", "blocker-content");

        var res = await MoveFileAsync(moving.Id, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task MoveFile_NameCollisionWithSiblingFolder_Returns409()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var dst = await CreateFolderAsync(root.Id, "dst");
        var moving = await CreateFileAsync(src.Id, "thing", "moving-content");
        await CreateFolderAsync(dst.Id, "thing");

        var res = await MoveFileAsync(moving.Id, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task MoveFile_TargetNotFound_Returns404()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var file = await CreateFileAsync(src.Id, "doc.txt", "hello-3");

        var res = await MoveFileAsync(file.Id, Guid.NewGuid());
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task MoveFile_EmptyParentId_Returns400()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "src");
        var file = await CreateFileAsync(folder.Id, "doc.txt", "hello-4");

        var res = await MoveFileAsync(file.Id, Guid.Empty);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ---------------------------------------------------------------------
    // MoveNode
    // ---------------------------------------------------------------------

    [Test]
    public async Task MoveNode_ToAnotherFolder_Succeeds()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var dst = await CreateFolderAsync(root.Id, "dst");
        var moving = await CreateFolderAsync(src.Id, "moving");

        var res = await MoveNodeAsync(moving.Id, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var dstChildren = await GetChildrenAsync(dst.Id);
        Assert.That(dstChildren.Nodes.Any(n => n.Id == moving.Id), Is.True);
    }

    [Test]
    public async Task MoveNode_SameParent_IsNoOp()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "folder");
        var child = await CreateFolderAsync(folder.Id, "child");

        var res = await MoveNodeAsync(child.Id, folder.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MoveNode_RootNode_Returns403()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var dst = await CreateFolderAsync(root.Id, "dst");

        var res = await MoveNodeAsync(root.Id, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task MoveNode_IntoSelf_Returns400()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "folder");

        var res = await MoveNodeAsync(folder.Id, folder.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task MoveNode_IntoDescendant_Returns400()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var parent = await CreateFolderAsync(root.Id, "parent");
        var middle = await CreateFolderAsync(parent.Id, "middle");
        var leaf = await CreateFolderAsync(middle.Id, "leaf");

        var res = await MoveNodeAsync(parent.Id, leaf.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task MoveNode_NameCollisionWithSiblingFolder_Returns409()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var dst = await CreateFolderAsync(root.Id, "dst");
        var moving = await CreateFolderAsync(src.Id, "thing");
        await CreateFolderAsync(dst.Id, "thing");

        var res = await MoveNodeAsync(moving.Id, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task MoveNode_NameCollisionWithSiblingFile_Returns409()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var dst = await CreateFolderAsync(root.Id, "dst");
        var moving = await CreateFolderAsync(src.Id, "thing");
        await CreateFileAsync(dst.Id, "thing", "blocker");

        var res = await MoveNodeAsync(moving.Id, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task MoveNode_TargetNotFound_Returns404()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "folder");

        var res = await MoveNodeAsync(folder.Id, Guid.NewGuid());
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task MoveNode_AcrossLayouts_Returns400()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var moving = await CreateFolderAsync(root.Id, "moving");

        // Same user, second layout: manufactured directly because the API only
        // exposes one auto-created layout per user. Use the factory's DI scope so
        // the DbContext is wired with the same NpgsqlDataSource as the app — a
        // bare `new DbContextOptionsBuilder().UseNpgsql(...)` trips Postgres type
        // OID lookups after the per-test EnsureDeleted+Create+migrations.
        Guid otherLayoutRootId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            var ownerId = await db.Users.AsNoTracking().Select(u => u.Id).FirstAsync();
            var newLayout = new Cotton.Database.Models.Layout { OwnerId = ownerId, IsActive = false };
            db.UserLayouts.Add(newLayout);
            await db.SaveChangesAsync();

            var newRoot = new Cotton.Database.Models.Node
            {
                LayoutId = newLayout.Id,
                OwnerId = ownerId,
                Type = Cotton.Database.Models.Enums.NodeType.Default,
                ParentId = null,
            };
            newRoot.SetName("other-root");
            db.Nodes.Add(newRoot);
            await db.SaveChangesAsync();
            otherLayoutRootId = newRoot.Id;
        }

        var res = await MoveNodeAsync(moving.Id, otherLayoutRootId);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ---------------------------------------------------------------------
    // Notification failure does not fail the move
    // ---------------------------------------------------------------------

    [Test]
    public async Task MoveFile_NotificationFailureDoesNotFailRequest()
    {
        // Reset the standard factory so we can wire a throwing notifier.
        _client?.Dispose();
        _factory?.Dispose();

        using var factory = new TestAppFactory(_overrides);
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IEventNotificationService));
                if (existing != null)
                {
                    services.Remove(existing);
                }
                services.AddScoped<IEventNotificationService, ThrowingEventNotificationService>();
            });
        });
        using var client = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Authenticate via this client.
        var token = await LoginViaClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        var src = await CreateFolderViaClientAsync(client, root!.Id, "src");
        var dst = await CreateFolderViaClientAsync(client, root.Id, "dst");
        var file = await CreateFileViaClientAsync(client, src.Id, "doc.txt", "fail-notify-content");

        var res = await client.PatchAsJsonAsync(
            $"/api/v1/files/{file.Id}/move",
            new MoveFileRequest { ParentId = dst.Id });

        // The handler must catch the notifier exception and still return 200.
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Notification failure must not turn a committed move into a failed response.");

        // Verify the move actually happened in DB.
        await using var db = NewReadOnlyDbContext();
        var moved = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == file.Id);
        Assert.That(moved.NodeId, Is.EqualTo(dst.Id));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private CottonDbContext NewReadOnlyDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CottonDbContext>();
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres",
            // Disable pooling so each test sees a fresh connection — between tests we
            // recreate the schema (EnsureDeleted + Create + migrations) and Postgres
            // type OIDs may change, which trips cached type lookups otherwise.
            Pooling = false,
        };
        optionsBuilder.UseNpgsql(csb.ConnectionString);
        return new CottonDbContext(optionsBuilder.Options);
    }

    private async Task AuthenticateAsync()
    {
        var token = await LoginViaClientAsync(_client!);
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<string> LoginViaClientAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto()
            {
                Username = "testuser",
                Password = "testpassword"
            })
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");
        var res = await client.SendAsync(request);
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        return login!.AccessToken;
    }

    private async Task<NodeDto> GetRootAsync()
    {
        var root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        return root!;
    }

    private Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
        => CreateFolderViaClientAsync(_client!, parentId, name);

    private static async Task<NodeDto> CreateFolderViaClientAsync(HttpClient client, Guid parentId, string name)
    {
        var res = await client.PutAsJsonAsync("/api/v1/layouts/nodes", new CreateNodeRequest { ParentId = parentId, Name = name });
        res.EnsureSuccessStatusCode();
        var node = await res.Content.ReadFromJsonAsync<NodeDto>();
        return node!;
    }

    private Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
        => CreateFileViaClientAsync(_client!, nodeId, name, body);

    private static async Task<NodeFileManifestDto> CreateFileViaClientAsync(HttpClient client, Guid nodeId, string name, string body)
    {
        var content = Encoding.UTF8.GetBytes(body);
        var hash = Hasher.ToHexStringHash(Hasher.HashData(content));
        using var form = new MultipartFormDataContent
        {
            {
                new ByteArrayContent(content)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                },
                "file",
                "chunk.bin"
            },
            { new StringContent(hash), "hash" }
        };
        var upRes = await client.PostAsync("/api/v1/chunks", form);
        upRes.EnsureSuccessStatusCode();

        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [hash],
            Name = name,
            ContentType = "application/octet-stream",
            Hash = hash,
            NodeId = nodeId
        };
        var createRes = await client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createRes.EnsureSuccessStatusCode();

        // The from-chunks endpoint returns 200 with no body; look the created file up by name.
        var children = await client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
        var dto = children!.Files.SingleOrDefault(f => f.Name == name)
            ?? throw new InvalidOperationException($"Created file '{name}' not found in node {nodeId}.");
        return dto;
    }

    private async Task<NodeContentDto> GetChildrenAsync(Guid nodeId)
    {
        var res = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
        return res!;
    }

    private Task<HttpResponseMessage> MoveFileAsync(Guid fileId, Guid parentId)
        => _client!.PatchAsJsonAsync($"/api/v1/files/{fileId}/move", new MoveFileRequest { ParentId = parentId });

    private Task<HttpResponseMessage> MoveNodeAsync(Guid nodeId, Guid parentId)
        => _client!.PatchAsJsonAsync($"/api/v1/layouts/nodes/{nodeId}/move", new MoveNodeRequest { ParentId = parentId });
}

internal sealed class ThrowingEventNotificationService : IEventNotificationService
{
    public Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyFileDeletedAsync(Guid userId, Guid nodeFileId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyFileMovedAsync(Guid nodeFileId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyNodeDeletedAsync(Guid userId, Guid nodeId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyNodeMovedAsync(Guid nodeId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
    public Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default) => throw new InvalidOperationException("simulated failure");
}
