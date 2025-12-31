// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Requests;
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

public class ChunksAndFilesEndpointsTests : IntegrationTestBase
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
    public async Task Upload_Chunk_And_Create_File_From_It_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // resolve root node
        var root = await _client!.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        // upload chunk
        var content = Encoding.UTF8.GetBytes("hello world");
        var chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
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
            { new StringContent(chunkHashLower), "hash" }
        };
        var upRes = await _client.PostAsync("/api/v1/chunks", form);
        upRes.EnsureSuccessStatusCode();

        // create file from chunk
        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [chunkHashLower],
            Name = "hello.txt",
            ContentType = "text/plain",
            Hash = chunkHashLower,
            NodeId = root!.Id
        };
        var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createFileRes.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Download_File_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // resolve root node
        var root = await _client!.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        // upload chunk
        var content = Encoding.UTF8.GetBytes("download me");
        var chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
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
            { new StringContent(chunkHashLower), "hash" }
        };
        var upRes = await _client.PostAsync("/api/v1/chunks", form);
        if (!upRes.IsSuccessStatusCode)
        {
            throw new Exception($"Chunk upload failed with status code {upRes.StatusCode} and message: {await upRes.Content.ReadAsStringAsync()}");
        }
        upRes.EnsureSuccessStatusCode();

        // create file from chunk
        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [chunkHashLower],
            Name = "download.txt",
            ContentType = "text/plain",
            Hash = chunkHashLower,
            NodeId = root!.Id
        };
        var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createFileRes.EnsureSuccessStatusCode();

        // list children to get NodeFileId
        var list = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
        Assert.That(list, Is.Not.Null);
        var nodeFile = list!.Files.FirstOrDefault(f => f.Name == "download.txt");
        Assert.That(nodeFile, Is.Not.Null);

        // download file
        var dl = await _client.GetAsync($"/api/v1/files/{nodeFile!.Id}/download");
        dl.EnsureSuccessStatusCode();
        var bytes = await dl.Content.ReadAsByteArrayAsync();
        Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("download me"));
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
        return login!.AccessToken;
    }
}
