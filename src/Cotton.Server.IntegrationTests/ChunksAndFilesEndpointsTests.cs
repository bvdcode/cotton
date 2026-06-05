// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests;

[NonParallelizable]
public class ChunksAndFilesEndpointsTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        ResetSettingsProviderCaches();

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
        ResetSettingsProviderCaches();
    }

    [Test]
    public async Task Upload_Chunk_And_Create_File_From_It_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // resolve root node
        var root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
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
            NodeId = root!.Id,
            Metadata = new Dictionary<string, string>
            {
                ["isClientEncrypted"] = "true",
                ["originalContentType"] = "text/plain"
            }
        };
        var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createFileRes.EnsureSuccessStatusCode();
        var created = await createFileRes.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(created.NodeId, Is.EqualTo(root!.Id));
        Assert.That(created.Name, Is.EqualTo("hello.txt"));

        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
        Assert.That(list, Is.Not.Null);
        var file = list!.Files.SingleOrDefault(x => x.Name == "hello.txt");
        Assert.That(file, Is.Not.Null);
        Assert.That(file!.Id, Is.EqualTo(created.Id));
        Assert.That(file.NodeId, Is.EqualTo(root.Id));
        Assert.That(file!.Metadata, Does.ContainKey("isClientEncrypted"));
        Assert.That(file.Metadata["isClientEncrypted"], Is.EqualTo("true"));
        Assert.That(file.Metadata["originalContentType"], Is.EqualTo("text/plain"));
    }

    [Test]
    public async Task Upload_Raw_Chunk_And_Create_File_From_It_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var content = Encoding.UTF8.GetBytes("hello raw world");
        var chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
        using var body = new ByteArrayContent(content)
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
        };

        var upRes = await _client.PostAsync($"/api/v1/chunks/raw?hash={chunkHashLower}", body);
        upRes.EnsureSuccessStatusCode();

        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [chunkHashLower],
            Name = "hello-raw.txt",
            ContentType = "text/plain",
            Hash = chunkHashLower,
            NodeId = root!.Id
        };
        var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createFileRes.EnsureSuccessStatusCode();

        var created = await createFileRes.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Name, Is.EqualTo("hello-raw.txt"));
    }

    [Test]
    public async Task Upload_Empty_Raw_Chunk_And_Create_Empty_File_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        byte[] content = [];
        string contentHash = Hasher.ToHexStringHash(Hasher.HashData(content));
        using var body = new ByteArrayContent(content)
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
        };

        var uploadResponse = await _client.PostAsync($"/api/v1/chunks/raw?hash={contentHash}", body);
        uploadResponse.EnsureSuccessStatusCode();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileRequest
        {
            ChunkHashes = [contentHash],
            Name = "empty-raw.txt",
            ContentType = "text/plain",
            Hash = contentHash,
            NodeId = root!.Id,
            Validate = true,
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Name, Is.EqualTo("empty-raw.txt"));
            Assert.That(created.SizeBytes, Is.Zero);
            Assert.That(created.ContentHash, Is.EqualTo(contentHash));
        });

        byte[] downloaded = await _client.GetByteArrayAsync($"/api/v1/files/{created!.Id}/content");
        Assert.That(downloaded, Is.Empty);
    }

    [Test]
    public async Task Create_File_Returns_Sync_Metadata_In_Create_Response_And_Children_List()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        byte[] content = Encoding.UTF8.GetBytes("sync metadata");
        string contentHash = Hasher.ToHexStringHash(Hasher.HashData(content));
        var uploadResponse = await UploadRawChunkAsync(content, contentHash);
        uploadResponse.EnsureSuccessStatusCode();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileRequest
        {
            ChunkHashes = [contentHash],
            Name = "sync-metadata.txt",
            ContentType = "text/plain",
            Hash = contentHash,
            NodeId = root!.Id,
            Validate = true,
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(created, Is.Not.Null);
        AssertSyncMetadata(created!, root.Id, contentHash);
        Assert.That(created!.OriginalNodeFileId, Is.EqualTo(created.Id));

        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root.Id}/children");
        Assert.That(list, Is.Not.Null);
        var listed = list!.Files.SingleOrDefault(x => x.Id == created.Id);
        Assert.That(listed, Is.Not.Null);
        AssertSyncMetadata(listed!, root.Id, contentHash);
        Assert.That(listed!.FileManifestId, Is.EqualTo(created.FileManifestId));
        Assert.That(listed.OriginalNodeFileId, Is.EqualTo(created.OriginalNodeFileId));
    }

    [Test]
    public async Task Download_Owned_File_Content_Works_With_Range_And_ETag()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "owned-content.txt", "0123456789abcdef");

        var download = await _client.GetAsync($"/api/v1/files/{file.Id}/content");
        download.EnsureSuccessStatusCode();
        Assert.That(download.Headers.ETag?.Tag, Is.EqualTo($"\"{file.ETag}\""));
        byte[] bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("0123456789abcdef"));

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{file.Id}/content");
        rangeRequest.Headers.Range = new RangeHeaderValue(4, 7);
        var range = await _client.SendAsync(rangeRequest);
        Assert.That(range.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
        byte[] rangeBytes = await range.Content.ReadAsByteArrayAsync();
        Assert.That(Encoding.UTF8.GetString(rangeBytes), Is.EqualTo("4567"));
    }

    [Test]
    public async Task WebDav_File_ETag_Uses_Same_Content_ETag_As_File_Api()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "webdav-etag.txt", "webdav content");
        string quotedETag = $"\"{file.ETag}\"";

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:testpassword")));

        var getResponse = await _client.GetAsync("/api/v1/webdav/webdav-etag.txt");
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, "/api/v1/webdav/webdav-etag.txt");
        var headResponse = await _client.SendAsync(headRequest);
        using var propFindRequest = new HttpRequestMessage(new HttpMethod("PROPFIND"), "/api/v1/webdav/webdav-etag.txt");
        propFindRequest.Headers.Add("Depth", "0");
        var propFindResponse = await _client.SendAsync(propFindRequest);
        string propFindXml = await propFindResponse.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(getResponse.Headers.ETag?.Tag, Is.EqualTo(quotedETag));
            Assert.That(headResponse.Headers.ETag?.Tag, Is.EqualTo(quotedETag));
            Assert.That(propFindResponse.StatusCode, Is.EqualTo(HttpStatusCode.MultiStatus));
            Assert.That(propFindXml, Does.Contain(quotedETag));
        });
    }

    [Test]
    public async Task Download_Owned_File_Content_Rejects_Another_User()
    {
        var ownerToken = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        var file = await UploadTextFileAsync(root!, "private-content.txt", "private");

        var createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
        {
            username = "synccontentuser",
            password = "synccontentpass",
            role = UserRole.User,
        });
        createUserResponse.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var otherToken = await LoginAsync("synccontentuser", "synccontentpass");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await _client.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Upload_Same_Chunk_In_Parallel_Deduplicates_Metadata()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        byte[] content = new byte[2 * 1024 * 1024];
        RandomNumberGenerator.Fill(content);
        string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));

        var responses = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => UploadRawChunkAsync(content, chunkHashLower)));

        foreach (var response in responses)
        {
            response.EnsureSuccessStatusCode();
            response.Dispose();
        }

        byte[] chunkHash = Hasher.FromHexStringHash(chunkHashLower);
        DbContext.ChangeTracker.Clear();
        int chunkCount = await DbContext.Chunks.CountAsync(x => x.Hash == chunkHash);
        int ownershipCount = await DbContext.ChunkOwnerships.CountAsync(x => x.ChunkHash == chunkHash);

        Assert.Multiple(() =>
        {
            Assert.That(chunkCount, Is.EqualTo(1));
            Assert.That(ownershipCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Create_And_Update_File_Reject_When_Default_User_Quota_Is_Exceeded()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var quotaResponse = await _client.PatchAsJsonAsync(
            "/api/v1/server/settings/default-user-storage-quota-bytes",
            5L);
        quotaResponse.EnsureSuccessStatusCode();

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        string fiveByteHash = await UploadChunkAndGetHashAsync("12345");
        var createFirstResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileRequest
        {
            ChunkHashes = [fiveByteHash],
            Name = "five.txt",
            ContentType = "text/plain",
            Hash = fiveByteHash,
            NodeId = root!.Id,
        });
        createFirstResponse.EnsureSuccessStatusCode();
        var created = await createFirstResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(created, Is.Not.Null);

        string sixByteHash = await UploadChunkAndGetHashAsync("abcdef");
        var createSecondResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileRequest
        {
            ChunkHashes = [sixByteHash],
            Name = "six.txt",
            ContentType = "text/plain",
            Hash = sixByteHash,
            NodeId = root.Id,
        });
        Assert.That(createSecondResponse.StatusCode, Is.EqualTo((HttpStatusCode)507));

        var updateResponse = await _client.PatchAsJsonAsync($"/api/v1/files/{created!.Id}/update-content", new CreateFileRequest
        {
            ChunkHashes = [sixByteHash],
            Name = "five.txt",
            ContentType = "text/plain",
            Hash = sixByteHash,
            NodeId = root.Id,
        });
        Assert.That(updateResponse.StatusCode, Is.EqualTo((HttpStatusCode)507));
    }

    [Test]
    public async Task User_Storage_Quota_Snapshot_Tracks_Create_And_Permanent_Delete_From_Cache()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var quotaResponse = await _client.PatchAsJsonAsync(
            "/api/v1/server/settings/default-user-storage-quota-bytes",
            100L);
        quotaResponse.EnsureSuccessStatusCode();

        var initialQuota = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.UserStorageQuotaDto>(
            "/api/v1/users/me/storage-quota");
        Assert.That(initialQuota, Is.Not.Null);
        Assert.That(initialQuota!.UsedBytes, Is.EqualTo(0));
        Assert.That(initialQuota.AvailableBytes, Is.EqualTo(100));

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "quota-cache.txt", "12345");
        var afterCreate = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.UserStorageQuotaDto>(
            "/api/v1/users/me/storage-quota");
        Assert.That(afterCreate, Is.Not.Null);
        Assert.That(afterCreate!.UsedBytes, Is.EqualTo(5));
        Assert.That(afterCreate.AvailableBytes, Is.EqualTo(95));

        var deleteResponse = await _client.DeleteAsync($"/api/v1/files/{file.Id}?skipTrash=true");
        deleteResponse.EnsureSuccessStatusCode();

        var afterDelete = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.UserStorageQuotaDto>(
            "/api/v1/users/me/storage-quota");
        Assert.That(afterDelete, Is.Not.Null);
        Assert.That(afterDelete!.UsedBytes, Is.EqualTo(0));
        Assert.That(afterDelete.AvailableBytes, Is.EqualTo(100));
    }

    [Test]
    public async Task Update_File_Content_With_Stale_If_Match_Returns_Precondition_Failed()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        NodeDto rootNode = root!;

        var file = await UploadTextFileAsync(rootNode, "etag-update.txt", "first");
        string staleETag = file.ETag;
        file = await UpdateTextFileAsync(file, rootNode, "second");
        string rejectedHash = await UploadChunkAndGetHashAsync("third");

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/files/{file.Id}/update-content")
        {
            Content = JsonContent.Create(new CreateFileRequest
            {
                ChunkHashes = [rejectedHash],
                Name = file.Name,
                ContentType = "text/plain",
                Hash = rejectedHash,
                NodeId = rootNode.Id,
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", staleETag);

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
    }

    [Test]
    public async Task Delete_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_File()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        NodeDto rootNode = root!;

        var file = await UploadTextFileAsync(rootNode, "etag-delete.txt", "first");
        string staleETag = file.ETag;
        file = await UpdateTextFileAsync(file, rootNode, "second");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/files/{file.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", staleETag);

        var response = await _client.SendAsync(request);
        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{rootNode.Id}/children");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
            Assert.That(list, Is.Not.Null);
            Assert.That(list!.Files.Select(x => x.Id), Does.Contain(file.Id));
        });
    }

    [Test]
    public async Task Rename_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_Name()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        NodeDto rootNode = root!;

        var file = await UploadTextFileAsync(rootNode, "etag-rename.txt", "first");
        string staleETag = file.ETag;
        file = await UpdateTextFileAsync(file, rootNode, "second");

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/files/{file.Id}/rename")
        {
            Content = JsonContent.Create(new RenameFileRequest { Name = "renamed.txt" })
        };
        request.Headers.TryAddWithoutValidation("If-Match", staleETag);

        var response = await _client.SendAsync(request);
        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{rootNode.Id}/children");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
            Assert.That(list, Is.Not.Null);
            Assert.That(list!.Files.Single(x => x.Id == file.Id).Name, Is.EqualTo("etag-rename.txt"));
        });
    }

    [Test]
    public async Task Move_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_Parent()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        NodeDto rootNode = root!;
        var destination = await CreateFolderAsync(rootNode.Id, "etag-move-destination");

        var file = await UploadTextFileAsync(rootNode, "etag-move.txt", "first");
        string staleETag = file.ETag;
        file = await UpdateTextFileAsync(file, rootNode, "second");

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/files/{file.Id}/move")
        {
            Content = JsonContent.Create(new MoveFileRequest { ParentId = destination.Id })
        };
        request.Headers.TryAddWithoutValidation("If-Match", staleETag);

        var response = await _client.SendAsync(request);
        var rootList = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{rootNode.Id}/children");
        var destinationList = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{destination.Id}/children");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
            Assert.That(rootList, Is.Not.Null);
            Assert.That(destinationList, Is.Not.Null);
            Assert.That(rootList!.Files.Select(x => x.Id), Does.Contain(file.Id));
            Assert.That(destinationList!.Files.Select(x => x.Id), Does.Not.Contain(file.Id));
        });
    }

    [Test]
    public async Task Admin_Created_User_Gets_Default_Template_Files()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        await UploadTextFileAsync(root!, "welcome.txt", "hello from the template");

        var templateResponse = await _client.PatchAsJsonAsync(
            "/api/v1/server/settings/default-user-template-node",
            root!.Id);
        templateResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
        {
            username = "seededuser",
            password = "seededpass",
            role = UserRole.User
        });
        createUserResponse.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var seededToken = await LoginAsync("seededuser", "seededpass");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seededToken);

        var seededRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(seededRoot, Is.Not.Null);

        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{seededRoot!.Id}/children");
        Assert.That(list, Is.Not.Null);
        var seededFile = list!.Files.SingleOrDefault(x => x.Name == "welcome.txt");
        Assert.That(seededFile, Is.Not.Null);
        Assert.That(seededFile!.SizeBytes, Is.EqualTo("hello from the template".Length));
    }

    [Test]
    public async Task Default_Template_Node_Rejects_Another_Users_Node()
    {
        var adminToken = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
        {
            username = "templateowner",
            password = "templatepass",
            role = UserRole.User
        });
        createUserResponse.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var otherToken = await LoginAsync("templateowner", "templatepass");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var otherRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(otherRoot, Is.Not.Null);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var templateResponse = await _client.PatchAsJsonAsync(
            "/api/v1/server/settings/default-user-template-node",
            otherRoot!.Id);

        Assert.That(templateResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Download_File_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // resolve root node
        var root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
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
        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
        Assert.That(list, Is.Not.Null);
        var nodeFile = list!.Files.FirstOrDefault(f => f.Name == "download.txt");
        Assert.That(nodeFile, Is.Not.Null);

        // obtain tokenized download link and download file
        var linkResponse = await _client.GetAsync($"/api/v1/files/{nodeFile!.Id}/download-link");
        linkResponse.EnsureSuccessStatusCode();
        var downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
        Assert.That(downloadLink, Is.Not.Null.And.Not.Empty);

        var dl = await _client.GetAsync(downloadLink);
        dl.EnsureSuccessStatusCode();
        var bytes = await dl.Content.ReadAsByteArrayAsync();
        Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("download me"));
    }

    [Test]
    public async Task Download_Archive_For_Selected_Files_Streams_Uncompressed_Zip_With_Utf8_Names()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var cyrillicFile = await UploadTextFileAsync(root!, "долги.txt", "рубли");
        var notesFile = await UploadTextFileAsync(root!, "notes.txt", "plain notes");

        var linkResponse = await _client.PostAsJsonAsync("/api/v1/archives/download-link", new Cotton.Server.Models.Requests.CreateArchiveDownloadLinkRequest
        {
            FileIds = [cyrillicFile.Id, notesFile.Id],
            NodeIds = [],
            ArchiveName = "выгрузка",
        });
        linkResponse.EnsureSuccessStatusCode();
        var archive = await linkResponse.Content.ReadFromJsonAsync<Cotton.Server.Models.Dto.ArchiveDownloadLinkDto>();
        Assert.That(archive, Is.Not.Null);
        Assert.That(archive!.FileName, Is.EqualTo("выгрузка.zip"));

        var download = await _client.GetAsync(archive.Url);
        download.EnsureSuccessStatusCode();
        Assert.That(download.Content.Headers.ContentLength, Is.EqualTo(archive.SizeBytes));

        byte[] bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.That(bytes.Length, Is.EqualTo(archive.SizeBytes));

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        AssertZipEntry(zip, "долги.txt", "рубли");
        AssertZipEntry(zip, "notes.txt", "plain notes");
        Assert.That(zip.GetEntry("долги.txt")!.CompressedLength, Is.EqualTo(zip.GetEntry("долги.txt")!.Length));
    }

    [Test]
    public async Task Download_Archive_For_Folder_Includes_Nested_Files_And_Empty_Folders()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var folder = await CreateFolderAsync(root!.Id, "Папка");
        var nested = await CreateFolderAsync(folder.Id, "nested");
        _ = await CreateFolderAsync(folder.Id, "empty");
        await UploadTextFileAsync(folder, "root.txt", "root body");
        await UploadTextFileAsync(nested, "deep.txt", "deep body");

        var linkResponse = await _client.PostAsJsonAsync("/api/v1/archives/download-link", new Cotton.Server.Models.Requests.CreateArchiveDownloadLinkRequest
        {
            FileIds = [],
            NodeIds = [folder.Id],
        });
        linkResponse.EnsureSuccessStatusCode();
        var archive = await linkResponse.Content.ReadFromJsonAsync<Cotton.Server.Models.Dto.ArchiveDownloadLinkDto>();
        Assert.That(archive, Is.Not.Null);
        Assert.That(archive!.FileName, Is.EqualTo("Папка.zip"));

        var download = await _client.GetAsync(archive.Url);
        download.EnsureSuccessStatusCode();
        byte[] bytes = await download.Content.ReadAsByteArrayAsync();

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.That(zip.GetEntry("Папка/"), Is.Not.Null);
        Assert.That(zip.GetEntry("Папка/empty/"), Is.Not.Null);
        Assert.That(zip.GetEntry("Папка/nested/"), Is.Not.Null);
        AssertZipEntry(zip, "Папка/root.txt", "root body");
        AssertZipEntry(zip, "Папка/nested/deep.txt", "deep body");
    }

    [Test]
    public async Task Download_Archive_Rejects_Another_Users_File()
    {
        var adminToken = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        var file = await UploadTextFileAsync(root!, "private.txt", "secret");

        var createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
        {
            username = "archiveuser",
            password = "archivepass",
            role = UserRole.User,
        });
        createUserResponse.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var otherToken = await LoginAsync("archiveuser", "archivepass");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var linkResponse = await _client.PostAsJsonAsync("/api/v1/archives/download-link", new Cotton.Server.Models.Requests.CreateArchiveDownloadLinkRequest
        {
            FileIds = [file.Id],
            NodeIds = [],
        });

        Assert.That(linkResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_File_Metadata_Merges_Metadata_For_Own_File()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(
            root!,
            "metadata.txt",
            "metadata",
            new Dictionary<string, string>
            {
                ["isClientEncrypted"] = "true",
                ["originalContentType"] = "text/plain"
            });

        var patch = new Dictionary<string, string>
        {
            ["en"] = "encrypted-display-name"
        };
        var updateRes = await _client.PatchAsJsonAsync($"/api/v1/files/{file.Id}/metadata", patch);
        updateRes.EnsureSuccessStatusCode();

        var updated = await updateRes.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Metadata["isClientEncrypted"], Is.EqualTo("true"));
        Assert.That(updated.Metadata["originalContentType"], Is.EqualTo("text/plain"));
        Assert.That(updated.Metadata["en"], Is.EqualTo("encrypted-display-name"));
    }

    [Test]
    public async Task Create_File_From_Chunks_Detects_ContentType_From_FileName_When_Missing()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var content = Encoding.UTF8.GetBytes("auto detect me");
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

        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [chunkHashLower],
            Name = "auto-detect.txt",
            ContentType = string.Empty,
            Hash = chunkHashLower,
            NodeId = root!.Id
        };
        var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createFileRes.EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
        Assert.That(list, Is.Not.Null);
        var file = list!.Files.FirstOrDefault(x => x.Name == "auto-detect.txt");
        Assert.That(file, Is.Not.Null);
        Assert.That(file!.ContentType, Is.EqualTo("text/plain"));
    }

    [Test]
    public async Task Share_RangeMetadataProbe_DoesNotConsume_DeleteAfterUse_Token()
    {
        var authToken = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "range-probe.txt", "0123456789abcdef");
        var linkResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link?deleteAfterUse=true");
        linkResponse.EnsureSuccessStatusCode();
        string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
        string shareToken = ExtractToken(downloadLink);

        _client.DefaultRequestHeaders.Authorization = null;
        using var probeRequest = new HttpRequestMessage(HttpMethod.Get, $"/s/{shareToken}?view=inline");
        probeRequest.Headers.Range = new RangeHeaderValue(0, 3);
        var probeResponse = await _client.SendAsync(probeRequest);
        Assert.That(probeResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.PartialContent));
        _ = await probeResponse.Content.ReadAsByteArrayAsync();

        DbContext.ChangeTracker.Clear();
        bool existsAfterProbe = await DbContext.DownloadTokens.AnyAsync(x => x.Token == shareToken);
        Assert.That(existsAfterProbe, Is.True);

        var downloadResponse = await _client.GetAsync($"/s/{shareToken}?view=download");
        downloadResponse.EnsureSuccessStatusCode();
        _ = await downloadResponse.Content.ReadAsByteArrayAsync();

        bool existsAfterDownload = await WaitForDownloadTokenAsync(shareToken, expectedExists: false);
        Assert.That(existsAfterDownload, Is.False);
    }

    [Test]
    public async Task File_Versions_List_Download_And_Restore_Previous_Content()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "versioned.txt", "first", new Dictionary<string, string>
        {
            ["originalContentType"] = "text/plain",
        });
        file = await UpdateTextFileAsync(file, root!, "second");
        file = await UpdateTextFileAsync(file, root!, "third");

        var versions = await GetVersionsAsync(file.Id);
        Assert.That(versions, Has.Count.EqualTo(3));
        Assert.That(versions[0].IsCurrent, Is.True);
        Assert.That(versions[0].VersionNumber, Is.EqualTo(3));

        FileVersionDto original = versions.Single(x => x.IsOriginal);
        Assert.Multiple(() =>
        {
            Assert.That(original.VersionNumber, Is.EqualTo(1));
            Assert.That(original.CanDelete, Is.False);
            Assert.That(versions.Single(x => x.VersionNumber == 2).CanDelete, Is.True);
        });

        string originalText = await DownloadVersionTextAsync(file.Id, original.Id);
        Assert.That(originalText, Is.EqualTo("first"));

        var restoreResponse = await _client.PostAsync($"/api/v1/files/{file.Id}/versions/{original.Id}/restore", null);
        restoreResponse.EnsureSuccessStatusCode();

        var currentLinkResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link");
        currentLinkResponse.EnsureSuccessStatusCode();
        string currentLink = (await currentLinkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
        var currentDownload = await _client.GetAsync(currentLink);
        currentDownload.EnsureSuccessStatusCode();
        string restoredText = Encoding.UTF8.GetString(await currentDownload.Content.ReadAsByteArrayAsync());
        Assert.That(restoredText, Is.EqualTo("first"));

        var versionsAfterRestore = await GetVersionsAsync(file.Id);
        Assert.Multiple(() =>
        {
            Assert.That(versionsAfterRestore, Has.Count.EqualTo(4));
            Assert.That(versionsAfterRestore[0].IsCurrent, Is.True);
            Assert.That(versionsAfterRestore[0].VersionNumber, Is.EqualTo(4));
            Assert.That(versionsAfterRestore.Single(x => x.IsOriginal).Id, Is.EqualTo(original.Id));
        });
    }

    [Test]
    public async Task File_Versions_Restore_Rejects_When_Restored_Copy_Would_Exceed_Quota()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var quotaResponse = await _client.PatchAsJsonAsync(
            "/api/v1/server/settings/default-user-storage-quota-bytes",
            10L);
        quotaResponse.EnsureSuccessStatusCode();

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "restore-quota.txt", "123456");
        file = await UpdateTextFileAsync(file, root!, "x");

        var versions = await GetVersionsAsync(file.Id);
        FileVersionDto original = versions.Single(x => x.IsOriginal);

        var restoreResponse = await _client.PostAsync($"/api/v1/files/{file.Id}/versions/{original.Id}/restore", null);
        Assert.That(restoreResponse.StatusCode, Is.EqualTo((HttpStatusCode)507));

        var quota = await _client.GetFromJsonAsync<UserStorageQuotaDto>("/api/v1/users/me/storage-quota");
        Assert.That(quota, Is.Not.Null);
        Assert.That(quota!.UsedBytes, Is.EqualTo(7));
    }

    [Test]
    public async Task File_Versions_Retention_Keeps_Original_And_Prunes_Oldest_Middle()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "retention.txt", "v0");
        for (int i = 1; i <= 12; i++)
        {
            file = await UpdateTextFileAsync(file, root!, "v" + i);
        }

        var versions = await GetVersionsAsync(file.Id);
        FileVersionDto original = versions.Single(x => x.IsOriginal);

        Assert.Multiple(() =>
        {
            Assert.That(versions, Has.Count.EqualTo(11));
            Assert.That(versions.Count(x => !x.IsCurrent), Is.EqualTo(10));
            Assert.That(versions[0].IsCurrent, Is.True);
            Assert.That(original.CanDelete, Is.False);
        });

        string originalText = await DownloadVersionTextAsync(file.Id, original.Id);
        Assert.That(originalText, Is.EqualTo("v0"));
    }

    [Test]
    public async Task File_Versions_Delete_Allows_NonOriginal_Only()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);

        var file = await UploadTextFileAsync(root!, "retained.txt", "one");
        file = await UpdateTextFileAsync(file, root!, "two");
        file = await UpdateTextFileAsync(file, root!, "three");

        var versions = await GetVersionsAsync(file.Id);
        FileVersionDto original = versions.Single(x => x.IsOriginal);
        FileVersionDto middle = versions.Single(x => !x.IsCurrent && !x.IsOriginal);

        Guid[] versionWrapperNodeIds = await DbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.Id == original.Id || x.Id == middle.Id)
            .Select(x => x.NodeId)
            .ToArrayAsync();

        var trashRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver?nodeType=Trash");
        Assert.That(trashRoot, Is.Not.Null);
        var directTrashContent = await _client.GetFromJsonAsync<NodeContentDto>(
            $"/api/v1/layouts/nodes/{trashRoot!.Id}/children?nodeType=Trash");
        var trashContent = await _client.GetFromJsonAsync<NodeContentDto>(
            $"/api/v1/layouts/nodes/{trashRoot.Id}/children?nodeType=Trash&depth=1");
        Assert.That(directTrashContent, Is.Not.Null);
        Assert.That(trashContent, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(directTrashContent!.Nodes.Select(x => x.Id).Intersect(versionWrapperNodeIds), Is.Empty);
            Assert.That(trashContent!.Files.Select(x => x.Id), Does.Not.Contain(original.Id));
            Assert.That(trashContent.Files.Select(x => x.Id), Does.Not.Contain(middle.Id));
        });

        var directDeleteOriginal = await _client.DeleteAsync($"/api/v1/files/{original.Id}?skipTrash=true");
        Assert.That(directDeleteOriginal.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var deleteOriginal = await _client.DeleteAsync($"/api/v1/files/{file.Id}/versions/{original.Id}");
        Assert.That(deleteOriginal.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var deleteMiddle = await _client.DeleteAsync($"/api/v1/files/{file.Id}/versions/{middle.Id}");
        deleteMiddle.EnsureSuccessStatusCode();

        var remaining = await GetVersionsAsync(file.Id);
        Assert.Multiple(() =>
        {
            Assert.That(remaining.Select(x => x.Id), Does.Not.Contain(middle.Id));
            Assert.That(remaining.Select(x => x.Id), Does.Contain(original.Id));
            Assert.That(remaining.Single(x => x.IsOriginal).CanDelete, Is.False);
        });
    }

    [Test]
    public async Task Folder_Permanent_Delete_Removes_File_Version_Lineages()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        Assert.That(root, Is.Not.Null);
        var folder = await CreateFolderAsync(root!.Id, "versioned-folder");
        var file = await UploadTextFileAsync(folder, "versioned-in-folder.txt", "one");
        file = await UpdateTextFileAsync(file, folder, "two");

        var versions = await GetVersionsAsync(file.Id);
        Assert.That(versions, Has.Count.EqualTo(2));

        Guid[] versionWrapperNodeIds = await DbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OriginalNodeFileId == file.Id && x.Id != file.Id)
            .Select(x => x.NodeId)
            .ToArrayAsync();
        Assert.That(versionWrapperNodeIds, Is.Not.Empty);

        var delete = await _client.DeleteAsync($"/api/v1/layouts/nodes/{folder.Id}?skipTrash=true");
        delete.EnsureSuccessStatusCode();

        DbContext.ChangeTracker.Clear();
        bool lineageExists = await DbContext.NodeFiles
            .AnyAsync(x => x.Id == file.Id || x.OriginalNodeFileId == file.Id);
        bool wrapperExists = await DbContext.Nodes
            .AnyAsync(x => versionWrapperNodeIds.Contains(x.Id));

        Assert.Multiple(() =>
        {
            Assert.That(lineageExists, Is.False);
            Assert.That(wrapperExists, Is.False);
        });
    }

    private static void AssertSyncMetadata(NodeFileManifestDto file, Guid expectedNodeId, string expectedContentHash)
    {
        Assert.Multiple(() =>
        {
            Assert.That(file.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file.NodeId, Is.EqualTo(expectedNodeId));
            Assert.That(file.FileManifestId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file.OriginalNodeFileId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file.ContentHash, Is.EqualTo(expectedContentHash));
            Assert.That(file.ETag, Is.EqualTo("sha256-" + expectedContentHash));
        });
    }

    private async Task<HttpResponseMessage> UploadRawChunkAsync(string text)
    {
        byte[] content = Encoding.UTF8.GetBytes(text);
        string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
        return await UploadRawChunkAsync(content, chunkHashLower);
    }

    private async Task<HttpResponseMessage> UploadRawChunkAsync(byte[] content, string chunkHashLower)
    {
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
        return await _client!.PostAsync("/api/v1/chunks", form);
    }

    private async Task<string> UploadChunkAndGetHashAsync(string text)
    {
        var response = await UploadRawChunkAsync(text);
        response.EnsureSuccessStatusCode();

        return Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static void ResetSettingsProviderCaches()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type settingsProviderType = typeof(SettingsProvider);

        settingsProviderType.GetField("_cache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_isServerInitializedCache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_serverHasUsersCache", flags)?.SetValue(null, null);
    }

    private async Task<List<FileVersionDto>> GetVersionsAsync(Guid fileId)
    {
        var versions = await _client!.GetFromJsonAsync<List<FileVersionDto>>($"/api/v1/files/{fileId}/versions");
        Assert.That(versions, Is.Not.Null);
        return versions!;
    }

    private async Task<string> DownloadVersionTextAsync(Guid fileId, Guid versionId)
    {
        var linkResponse = await _client!.GetAsync($"/api/v1/files/{fileId}/versions/{versionId}/download-link");
        linkResponse.EnsureSuccessStatusCode();
        string link = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
        var download = await _client.GetAsync(link);
        download.EnsureSuccessStatusCode();
        return Encoding.UTF8.GetString(await download.Content.ReadAsByteArrayAsync());
    }

    private async Task<NodeFileManifestDto> UpdateTextFileAsync(
        NodeFileManifestDto file,
        NodeDto root,
        string text)
    {
        string hash = await UploadChunkAndGetHashAsync(text);
        var updateResponse = await _client!.PatchAsJsonAsync($"/api/v1/files/{file.Id}/update-content", new CreateFileRequest
        {
            ChunkHashes = [hash],
            Name = file.Name,
            ContentType = "text/plain",
            Hash = hash,
            NodeId = root.Id,
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(updated, Is.Not.Null);
        return updated!;
    }

    private async Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
    {
        var response = await _client!.PutAsJsonAsync(
            "/api/v1/layouts/nodes",
            new Cotton.Server.Models.Requests.CreateNodeRequest { ParentId = parentId, Name = name });
        response.EnsureSuccessStatusCode();
        var node = await response.Content.ReadFromJsonAsync<NodeDto>();
        Assert.That(node, Is.Not.Null);
        return node!;
    }

    private static void AssertZipEntry(ZipArchive zip, string path, string expectedText)
    {
        ZipArchiveEntry? entry = zip.GetEntry(path);
        Assert.That(entry, Is.Not.Null, $"Archive entry '{path}' was not found.");
        using Stream stream = entry!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.That(reader.ReadToEnd(), Is.EqualTo(expectedText));
    }

    private async Task<NodeFileManifestDto> UploadTextFileAsync(
        NodeDto root,
        string name,
        string text,
        Dictionary<string, string>? metadata = null)
    {
        var content = Encoding.UTF8.GetBytes(text);
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
        var upRes = await _client!.PostAsync("/api/v1/chunks", form);
        upRes.EnsureSuccessStatusCode();

        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [chunkHashLower],
            Name = name,
            ContentType = "text/plain",
            Hash = chunkHashLower,
            NodeId = root.Id,
            Metadata = metadata
        };
        var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createFileRes.EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root.Id}/children");
        Assert.That(list, Is.Not.Null);
        var file = list!.Files.SingleOrDefault(x => x.Name == name);
        Assert.That(file, Is.Not.Null);
        return file!;
    }

    private static string ExtractToken(string downloadLink)
    {
        const string marker = "token=";
        int index = downloadLink.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        return Uri.UnescapeDataString(downloadLink[(index + marker.Length)..]);
    }

    private async Task<bool> WaitForDownloadTokenAsync(string token, bool expectedExists)
    {
        for (int i = 0; i < 20; i++)
        {
            DbContext.ChangeTracker.Clear();
            bool exists = await DbContext.DownloadTokens.AnyAsync(x => x.Token == token);
            if (exists == expectedExists)
            {
                return exists;
            }

            await Task.Delay(50);
        }

        DbContext.ChangeTracker.Clear();
        return await DbContext.DownloadTokens.AnyAsync(x => x.Token == token);
    }

    private async Task<string> LoginAsync(
        string username = "testuser",
        string password = "testpassword")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto()
            {
                Username = username,
                Password = password
            })
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");
        var res = await _client!.SendAsync(request);
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
