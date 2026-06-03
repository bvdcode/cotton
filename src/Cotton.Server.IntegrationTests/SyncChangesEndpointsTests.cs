// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
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

[NonParallelizable]
public sealed class SyncChangesEndpointsTests : IntegrationTestBase
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
    public async Task Changes_ReturnsOrderedMutationPagesAfterCursor()
    {
        string token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        SyncChangesResponseDto initial = await GetChangesAsync(0, 10);
        Assert.That(initial.Changes, Is.Empty);

        NodeDto folder = await CreateNodeAsync(root!.Id, "feed-folder");
        NodeFileManifestDto file = await CreateFileAsync(folder.Id, "feed.txt", "first");
        NodeFileManifestDto renamed = await RenameFileAsync(file.Id, "feed-renamed.txt");
        await DeleteFileAsync(renamed.Id);

        SyncChangesResponseDto firstPage = await GetChangesAsync(0, 2);
        Assert.Multiple(() =>
        {
            Assert.That(firstPage.HasMore, Is.True);
            Assert.That(firstPage.Changes.Select(x => x.Kind), Is.EqualTo(new[]
            {
                SyncChangeKindDto.FolderCreated,
                SyncChangeKindDto.FileCreated,
            }));
            Assert.That(firstPage.Changes[0].NodeId, Is.EqualTo(folder.Id));
            Assert.That(firstPage.Changes[1].NodeFileId, Is.EqualTo(file.Id));
        });

        SyncChangesResponseDto secondPage = await GetChangesAsync(firstPage.NextCursor, 10);
        Assert.Multiple(() =>
        {
            Assert.That(secondPage.HasMore, Is.False);
            Assert.That(secondPage.Changes.Select(x => x.Kind), Is.EqualTo(new[]
            {
                SyncChangeKindDto.FileRenamed,
                SyncChangeKindDto.FileDeleted,
            }));
            Assert.That(secondPage.Changes[0].NodeFileId, Is.EqualTo(file.Id));
            Assert.That(secondPage.Changes[0].Name, Is.EqualTo("feed-renamed.txt"));
            Assert.That(secondPage.Changes[1].NodeFileId, Is.EqualTo(file.Id));
            Assert.That(secondPage.Changes[1].PreviousParentNodeId, Is.EqualTo(folder.Id));
            Assert.That(secondPage.NextCursor, Is.GreaterThan(firstPage.NextCursor));
        });
    }

    private async Task<SyncChangesResponseDto> GetChangesAsync(long since, int limit)
    {
        var page = await _client!.GetFromJsonAsync<SyncChangesResponseDto>(
            $"/api/v1/sync/changes?since={since}&limit={limit}");
        Assert.That(page, Is.Not.Null);
        return page!;
    }

    private async Task<NodeDto> CreateNodeAsync(Guid parentId, string name)
    {
        var response = await _client!.PutAsJsonAsync(
            "/api/v1/layouts/nodes",
            new Models.Requests.CreateNodeRequest { ParentId = parentId, Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodeDto>())!;
    }

    private async Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
    {
        string hash = await UploadChunkAsync(body);
        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [hash],
            Name = name,
            ContentType = "text/plain",
            Hash = hash,
            NodeId = nodeId,
        };
        var response = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
    }

    private async Task<NodeFileManifestDto> RenameFileAsync(Guid nodeFileId, string name)
    {
        var response = await _client!.PatchAsJsonAsync(
            $"/api/v1/files/{nodeFileId}/rename",
            new Models.Requests.RenameFileRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
    }

    private async Task DeleteFileAsync(Guid nodeFileId)
    {
        var response = await _client!.DeleteAsync($"/api/v1/files/{nodeFileId}");
        response.EnsureSuccessStatusCode();
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

        var response = await _client!.PostAsync("/api/v1/chunks", form);
        response.EnsureSuccessStatusCode();
        return hash;
    }

    private async Task<string> LoginAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto
            {
                Username = "testuser",
                Password = "testpassword"
            })
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");
        var response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
