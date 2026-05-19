// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;

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
            NodeId = root!.Id,
            Metadata = new Dictionary<string, string>
            {
                ["isClientEncrypted"] = "true",
                ["originalContentType"] = "text/plain"
            }
        };
        var createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        createFileRes.EnsureSuccessStatusCode();
        var created = await createFileRes.Content.ReadFromJsonAsync<Cotton.Server.Models.Dto.NodeFileManifestDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(created.NodeId, Is.EqualTo(root!.Id));
        Assert.That(created.Name, Is.EqualTo("hello.txt"));

        var list = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
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
    public async Task Create_And_Update_File_Reject_When_Default_User_Quota_Is_Exceeded()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var quotaResponse = await _client.PatchAsJsonAsync(
            "/api/v1/server/settings/default-user-storage-quota-bytes",
            5L);
        quotaResponse.EnsureSuccessStatusCode();

        var root = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
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
        var created = await createFirstResponse.Content.ReadFromJsonAsync<Cotton.Server.Models.Dto.NodeFileManifestDto>();
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
        Assert.That(createSecondResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));

        var updateResponse = await _client.PatchAsJsonAsync($"/api/v1/files/{created!.Id}/update-content", new CreateFileRequest
        {
            ChunkHashes = [sixByteHash],
            Name = "five.txt",
            ContentType = "text/plain",
            Hash = sixByteHash,
            NodeId = root.Id,
        });
        Assert.That(updateResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
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

        var root = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
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
    public async Task Admin_Created_User_Gets_Default_Template_Files()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
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

        var seededRoot = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
        Assert.That(seededRoot, Is.Not.Null);

        var list = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.NodeContentDto>($"/api/v1/layouts/nodes/{seededRoot!.Id}/children");
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

        var otherRoot = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
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
    public async Task Update_File_Metadata_Merges_Metadata_For_Own_File()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
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

        var updated = await updateRes.Content.ReadFromJsonAsync<Cotton.Server.Models.Dto.NodeFileManifestDto>();
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

        var root = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
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

        var list = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
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

        var root = await _client.GetFromJsonAsync<Models.Dto.NodeDto>("/api/v1/layouts/resolver");
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

    private async Task<HttpResponseMessage> UploadRawChunkAsync(string text)
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

    private async Task<Cotton.Server.Models.Dto.NodeFileManifestDto> UploadTextFileAsync(
        Models.Dto.NodeDto root,
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

        var list = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.NodeContentDto>($"/api/v1/layouts/nodes/{root.Id}/children");
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
