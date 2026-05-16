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
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task MoveFile_AcrossLayouts_Returns400()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var src = await CreateFolderAsync(root.Id, "src");
        var file = await CreateFileAsync(src.Id, "doc.txt", "across-layouts");

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

        var res = await MoveFileAsync(file.Id, otherLayoutRootId);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
    public async Task MoveNode_ConcurrentSwap_DoesNotCreateCycle()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var a = await CreateFolderAsync(root.Id, "a");
        var b = await CreateFolderAsync(root.Id, "b");

        // Without the per-layout advisory lock, both descendant checks could pass
        // on the pre-update tree and both commits would land — leaving A.parent=B
        // and B.parent=A. With the lock the second request re-runs the descendant
        // check inside the lock and rejects as into-descendant.
        var moveAIntoB = MoveNodeAsync(a.Id, b.Id);
        var moveBIntoA = MoveNodeAsync(b.Id, a.Id);
        var results = await Task.WhenAll(moveAIntoB, moveBIntoA);

        int oks = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        int bads = results.Count(r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.That(oks, Is.EqualTo(1), "Exactly one swap leg must succeed.");
        Assert.That(bads, Is.EqualTo(1), "The losing leg must be rejected (into-descendant).");

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
        Assert.That(await ParentWalkReachesRoot(db, a.Id), Is.True, "A must reach the root with no cycle.");
        Assert.That(await ParentWalkReachesRoot(db, b.Id), Is.True, "B must reach the root with no cycle.");
    }

    private static async Task<bool> ParentWalkReachesRoot(CottonDbContext db, Guid startId)
    {
        var seen = new HashSet<Guid>();
        Guid? current = startId;
        while (current.HasValue)
        {
            if (!seen.Add(current.Value)) return false;
            if (seen.Count > 1024) return false;
            current = await db.Nodes
                .AsNoTracking()
                .Where(n => n.Id == current.Value)
                .Select(n => n.ParentId)
                .SingleOrDefaultAsync();
        }
        return true;
    }

    [Test]
    public async Task MoveNode_NonDefaultType_Returns404()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var dst = await CreateFolderAsync(root.Id, "dst");

        // Build a Trash-type sibling under root via the DI scope — the API does
        // not expose creation of non-Default nodes.
        Guid trashNodeId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            var ownerId = await db.Users.AsNoTracking().Select(u => u.Id).FirstAsync();
            var rootEntity = await db.Nodes.AsNoTracking().SingleAsync(n => n.Id == root.Id);
            var trash = new Cotton.Database.Models.Node
            {
                LayoutId = rootEntity.LayoutId,
                OwnerId = ownerId,
                Type = Cotton.Database.Models.Enums.NodeType.Trash,
                ParentId = rootEntity.Id,
            };
            trash.SetName("trash-thing");
            db.Nodes.Add(trash);
            await db.SaveChangesAsync();
            trashNodeId = trash.Id;
        }

        var res = await MoveNodeAsync(trashNodeId, dst.Id);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
            "Move endpoint must reject non-Default node types as not-found (no leak).");
    }

    [Test]
    public async Task MoveNode_AcrossLayouts_Returns400()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var moving = await CreateFolderAsync(root.Id, "moving");

        // Same user, second layout: API only auto-creates one layout per user, so
        // we manufacture the second one directly via the factory's DI scope.
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
    public async Task WebDavMove_NotificationFailureDoesNotFailRequest()
    {
        // Reset the standard factory so we can wire a throwing notifier.
        _client?.Dispose();
        _factory?.Dispose();

        using var factory = new TestAppFactory(_overrides);
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEventNotificationService>();
                services.AddScoped<IEventNotificationService, ThrowingEventNotificationService>();
            });
        });
        using var client = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Provision the user + source/destination folders + a file via REST first.
        var token = await LoginViaClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var root = await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        var src = await CreateFolderViaClientAsync(client, root!.Id, "src");
        var dst = await CreateFolderViaClientAsync(client, root.Id, "dst");
        var file = await CreateFileViaClientAsync(client, src.Id, "doc.txt", "webdav-fail-notify");

        // Switch to WebDAV basic auth for the MOVE request.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:testpassword")));

        using var moveRequest = new HttpRequestMessage(new HttpMethod("MOVE"), "/api/v1/webdav/src/doc.txt");
        moveRequest.Headers.Add("Destination", "/api/v1/webdav/dst/doc.txt");
        moveRequest.Headers.Add("Overwrite", "F");
        var res = await client.SendAsync(moveRequest);

        // WebDAV MOVE returns 201 Created when the destination did not previously exist,
        // or 204 NoContent on overwrite. Either is success — but it MUST NOT fail
        // because the realtime notifier threw after the move already committed.
        Assert.That((int)res.StatusCode, Is.AnyOf(201, 204),
            $"WebDAV MOVE must succeed despite notification failure (got {(int)res.StatusCode}).");

        using (var scope = customFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            var moved = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == file.Id);
            Assert.That(moved.NodeId, Is.EqualTo(dst.Id), "File must have been moved despite notification failure.");
        }
    }

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
                services.RemoveAll<IEventNotificationService>();
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
