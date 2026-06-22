// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class LayoutEndpointsTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
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
            ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
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

        NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        // create child
        HttpResponseMessage createNodeRes = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", new CreateNodeRequestDto { ParentId = root!.Id, Name = "child" });
        createNodeRes.EnsureSuccessStatusCode();
        NodeDto? child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
        Assert.That(child, Is.Not.Null);

        // resolve path
        NodeDto? resolved = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver/child");
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Id, Is.EqualTo(child!.Id));

        // list ancestors
        IEnumerable<NodeDto>? ancestors = await _client.GetFromJsonAsync<IEnumerable<NodeDto>>($"/api/v1/layouts/nodes/{child!.Id}/ancestors");
        Assert.That(ancestors, Is.Not.Null);
        Assert.That(ancestors!.Any(a => a.Id == root!.Id), Is.True);

        // list children for root
        NodeContentDto? children = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
        Assert.That(children, Is.Not.Null);
        Assert.That(children!.Nodes.Any(n => n.Id == child!.Id), Is.True);
    }

    [Test]
    public async Task Update_Node_Metadata_Merges_And_Persists_String_Values()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        HttpResponseMessage createNodeRes = await _client.PutAsJsonAsync(
            "/api/v1/layouts/nodes",
            new CreateNodeRequestDto { ParentId = root!.Id, Name = "encrypted" });
        createNodeRes.EnsureSuccessStatusCode();
        NodeDto? child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
        Assert.That(child, Is.Not.Null);

        HttpResponseMessage firstPatch = await _client.PatchAsJsonAsync(
            $"/api/v1/layouts/nodes/{child!.Id}/metadata",
            new Dictionary<string, string>
            {
                ["isClientEncryptionEnabled"] = "true",
                ["color"] = "blue"
            });
        firstPatch.EnsureSuccessStatusCode();
        NodeDto? first = await firstPatch.Content.ReadFromJsonAsync<NodeDto>();

        Assert.Multiple(() =>
        {
            Assert.That(first!.Metadata["isClientEncryptionEnabled"], Is.EqualTo("true"));
            Assert.That(first.Metadata["color"], Is.EqualTo("blue"));
        });

        HttpResponseMessage secondPatch = await _client.PatchAsJsonAsync(
            $"/api/v1/layouts/nodes/{child.Id}/metadata",
            new Dictionary<string, string>
            {
                ["isClientEncryptionEnabled"] = "false"
            });
        secondPatch.EnsureSuccessStatusCode();
        NodeDto? second = await secondPatch.Content.ReadFromJsonAsync<NodeDto>();

        Assert.Multiple(() =>
        {
            Assert.That(second!.Metadata["isClientEncryptionEnabled"], Is.EqualTo("false"));
            Assert.That(second.Metadata["color"], Is.EqualTo("blue"));
        });

        NodeDto? persisted = await _client.GetFromJsonAsync<NodeDto>($"/api/v1/layouts/nodes/{child.Id}");
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Metadata["isClientEncryptionEnabled"], Is.EqualTo("false"));
            Assert.That(persisted.Metadata["color"], Is.EqualTo("blue"));
        });
    }

    [Test]
    public async Task Search_ByNodeGuid_ReturnsOnlyExactNode()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        HttpResponseMessage targetResponse = await _client.PutAsJsonAsync(
            "/api/v1/layouts/nodes",
            new CreateNodeRequestDto { ParentId = root!.Id, Name = "target" });
        targetResponse.EnsureSuccessStatusCode();
        NodeDto? target = await targetResponse.Content.ReadFromJsonAsync<NodeDto>();
        Assert.That(target, Is.Not.Null);

        HttpResponseMessage textMatchResponse = await _client.PutAsJsonAsync(
            "/api/v1/layouts/nodes",
            new CreateNodeRequestDto { ParentId = root.Id, Name = "why-log" });
        textMatchResponse.EnsureSuccessStatusCode();

        SearchLayoutsResultDto exact = await SearchAsync(root.LayoutId, target!.Id.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(exact.TotalCount, Is.EqualTo(1));
            Assert.That(exact.Nodes.Single().Id, Is.EqualTo(target.Id));
            Assert.That(exact.Files, Is.Empty);
        });

        SearchLayoutsResultDto copiedLogLine = await SearchAsync(root.LayoutId, $"{target.Id} why");
        Assert.Multiple(() =>
        {
            Assert.That(copiedLogLine.TotalCount, Is.EqualTo(1));
            Assert.That(copiedLogLine.Nodes.Single().Id, Is.EqualTo(target.Id));
            Assert.That(copiedLogLine.Files, Is.Empty);
        });
    }

    [Test]
    public async Task Search_ByText_ReturnsFoldersAndFilesWithPathsAndPagination()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        NodeDto exactFolder = await CreateNodeAsync(root!.Id, "demo");
        NodeDto fileParent = await CreateNodeAsync(root.Id, "file-parent");
        NodeFileManifestDto exactFile = await CreateFileAsync(fileParent.Id, "demo", "file exact");
        NodeDto prefixFolder = await CreateNodeAsync(root.Id, "demo archive");
        NodeDto substringFolder = await CreateNodeAsync(root.Id, "old demo backup");

        SearchLayoutsResultDto firstPage = await SearchAsync(root.LayoutId, "demo", page: 1, pageSize: 2);
        Assert.Multiple(() =>
        {
            Assert.That(firstPage.TotalCount, Is.EqualTo(4));
            Assert.That(firstPage.Nodes.Single().Id, Is.EqualTo(exactFolder.Id));
            Assert.That(firstPage.Files.Single().Id, Is.EqualTo(exactFile.Id));
            Assert.That(firstPage.NodePaths[exactFolder.Id], Is.EqualTo($"/{root.Name}/demo"));
            Assert.That(firstPage.FilePaths[exactFile.Id], Is.EqualTo($"/{root.Name}/file-parent/demo"));
        });

        SearchLayoutsResultDto secondPage = await SearchAsync(root.LayoutId, "demo", page: 2, pageSize: 2);
        Assert.Multiple(() =>
        {
            Assert.That(secondPage.TotalCount, Is.EqualTo(4));
            Assert.That(secondPage.Files, Is.Empty);
            Assert.That(secondPage.Nodes.Select(x => x.Id), Is.EqualTo(new[]
            {
                prefixFolder.Id,
                substringFolder.Id,
            }));
        });
    }

    [Test]
    public async Task Search_DoesNotReturnTrashedNodesOrFiles()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        NodeDto visible = await CreateNodeAsync(root!.Id, "archive-visible");
        NodeDto trashedFolder = await CreateNodeAsync(root.Id, "archive-trash-folder");
        _ = await CreateNodeAsync(trashedFolder.Id, "archive-trash-child");
        NodeFileManifestDto trashedFile = await CreateFileAsync(root.Id, "archive-trash-file.txt", "trash me");

        (await _client.DeleteAsync($"/api/v1/layouts/nodes/{trashedFolder.Id}")).EnsureSuccessStatusCode();
        (await _client.DeleteAsync($"/api/v1/files/{trashedFile.Id}")).EnsureSuccessStatusCode();

        SearchLayoutsResultDto visibleResult = await SearchAsync(root.LayoutId, "archive-visible");
        SearchLayoutsResultDto trashResult = await SearchAsync(root.LayoutId, "archive-trash");

        Assert.Multiple(() =>
        {
            Assert.That(visibleResult.TotalCount, Is.EqualTo(1));
            Assert.That(visibleResult.Nodes.Single().Id, Is.EqualTo(visible.Id));
            Assert.That(trashResult.TotalCount, Is.EqualTo(0));
            Assert.That(trashResult.Nodes, Is.Empty);
            Assert.That(trashResult.Files, Is.Empty);
        });
    }

    private async Task<SearchLayoutsResultDto> SearchAsync(Guid layoutId, string query, int page = 1, int pageSize = 20)
    {
        HttpResponseMessage response = await _client!.GetAsync(
            $"/api/v1/layouts/{layoutId}/search?query={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchLayoutsResultDto>())!;
    }

    private async Task<NodeDto> CreateNodeAsync(Guid parentId, string name)
    {
        HttpResponseMessage response = await _client!.PutAsJsonAsync(
            "/api/v1/layouts/nodes",
            new CreateNodeRequestDto { ParentId = parentId, Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodeDto>())!;
    }

    private async Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
    {
        string hash = await UploadChunkAsync(body);
        var fileReq = new CreateFileFromChunksRequestDto
        {
            ChunkHashes = [hash],
            Name = name,
            ContentType = "application/octet-stream",
            Hash = hash,
            NodeId = nodeId,
        };
        HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        response.EnsureSuccessStatusCode();

        NodeContentDto? children = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
        return children!.Files.Single(x => x.Name == name);
    }

    private async Task<string> UploadChunkAsync(string body)
    {
        byte[] content = Encoding.UTF8.GetBytes(body);
        string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
        using var form = new MultipartFormDataContent
        {
            {
                new ByteArrayContent(content)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") },
                },
                "file",
                "chunk.bin"
            },
            { new StringContent(hash), "hash" },
        };

        HttpResponseMessage response = await _client!.PostAsync("/api/v1/chunks", form);
        response.EnsureSuccessStatusCode();
        return hash;
    }

    private async Task<string> LoginAsync()
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
        HttpResponseMessage res = await _client!.SendAsync(request);
        res.EnsureSuccessStatusCode();
        TokenPairResponseDto? login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
