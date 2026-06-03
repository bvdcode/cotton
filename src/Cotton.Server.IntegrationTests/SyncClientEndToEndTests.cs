// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net.Http.Json;
using System.Text;
using Cotton.Contracts.Auth;
using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Sdk;
using Cotton.Sdk.Auth;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Cotton.Sync;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

[NonParallelizable]
public sealed class SyncClientEndToEndTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _httpClient;
    private string _tempDirectory = string.Empty;

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

        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-sync-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        _factory = new TestAppFactory(CreateServerOverrides());
        _httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task RunOnceAsync_UploadsLocalFileThroughSdkToServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e");
        string localRoot = Path.Combine(_tempDirectory, "client-a");
        WriteLocalFile(localRoot, "Docs/hello.txt", "hello from sync core");
        SqliteSyncStateStore stateStore = CreateStateStore("client-a-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);

        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "client-a",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        });

        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, "Docs", "hello.txt");
        string downloaded = await DownloadTextAsync(client, uploaded.Id);
        SyncStateEntry? baseline = await stateStore.GetAsync("client-a", "Docs/hello.txt");

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(downloaded, Is.EqualTo("hello from sync core"));
            Assert.That(uploaded.ContentHash, Is.EqualTo(baseline?.RemoteContentHash));
            Assert.That(baseline?.LocalContentHash, Is.EqualTo(uploaded.ContentHash));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_UploadsLocalUpdateThroughSdkToServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-update");
        string localRoot = Path.Combine(_tempDirectory, "client-update");
        const string relativePath = "Docs/update.txt";
        WriteLocalFile(localRoot, relativePath, "first version");
        SqliteSyncStateStore stateStore = CreateStateStore("client-update-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-update",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };
        await engine.RunOnceAsync(syncPair);
        NodeFileManifestDto initial = await FindRemoteFileAsync(client, remoteRoot.Id, "Docs", "update.txt");

        WriteLocalFile(localRoot, relativePath, "second version");
        SyncRunResult result = await engine.RunOnceAsync(syncPair);

        NodeFileManifestDto updated = await FindRemoteFileAsync(client, remoteRoot.Id, "Docs", "update.txt");
        string downloaded = await DownloadTextAsync(client, updated.Id);
        SyncStateEntry? baseline = await stateStore.GetAsync("client-update", relativePath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(downloaded, Is.EqualTo("second version"));
            Assert.That(updated.Id, Is.EqualTo(initial.Id));
            Assert.That(updated.ContentHash, Is.Not.EqualTo(initial.ContentHash));
            Assert.That(baseline?.LocalContentHash, Is.EqualTo(updated.ContentHash));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(updated.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteCreatedFileThroughSdkToLocal()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-download");
        NodeDto remoteDirectory = await client.Nodes.CreateAsync(remoteRoot.Id, "Docs");
        NodeFileManifestDto remoteFile = await CreateRemoteTextFileAsync(
            client,
            remoteDirectory.Id,
            "remote.txt",
            "remote-created content");
        string localRoot = Path.Combine(_tempDirectory, "client-download");
        Directory.CreateDirectory(localRoot);
        SqliteSyncStateStore stateStore = CreateStateStore("client-download-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);

        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "client-download",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        });

        string localContent = File.ReadAllText(Path.Combine(localRoot, "Docs", "remote.txt"), Encoding.UTF8);
        SyncStateEntry? baseline = await stateStore.GetAsync("client-download", "Docs/remote.txt");

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(localContent, Is.EqualTo("remote-created content"));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(remoteFile.Id));
            Assert.That(baseline?.LocalContentHash, Is.EqualTo(remoteFile.ContentHash));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(remoteFile.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteUpdateThroughSdkToLocal()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-remote-update");
        NodeDto remoteDirectory = await client.Nodes.CreateAsync(remoteRoot.Id, "Docs");
        NodeFileManifestDto remoteFile = await CreateRemoteTextFileAsync(
            client,
            remoteDirectory.Id,
            "remote-update.txt",
            "remote first");
        string localRoot = Path.Combine(_tempDirectory, "client-remote-update");
        Directory.CreateDirectory(localRoot);
        SqliteSyncStateStore stateStore = CreateStateStore("client-remote-update-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-remote-update",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };
        await engine.RunOnceAsync(syncPair);

        NodeFileManifestDto updatedRemoteFile = await UpdateRemoteTextFileAsync(client, remoteFile, "remote second");
        SyncRunResult result = await engine.RunOnceAsync(syncPair);

        string localContent = File.ReadAllText(Path.Combine(localRoot, "Docs", "remote-update.txt"), Encoding.UTF8);
        SyncStateEntry? baseline = await stateStore.GetAsync("client-remote-update", "Docs/remote-update.txt");

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(localContent, Is.EqualTo("remote second"));
            Assert.That(updatedRemoteFile.Id, Is.EqualTo(remoteFile.Id));
            Assert.That(updatedRemoteFile.ContentHash, Is.Not.EqualTo(remoteFile.ContentHash));
            Assert.That(baseline?.LocalContentHash, Is.EqualTo(updatedRemoteFile.ContentHash));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(updatedRemoteFile.ContentHash));
        });
    }

    private CottonCloudClient CreateClient()
    {
        Assert.That(_httpClient, Is.Not.Null);
        Assert.That(_httpClient!.BaseAddress, Is.Not.Null);
        return new CottonCloudClient(
            _httpClient,
            new InMemoryCottonTokenStore(),
            new CottonSdkOptions
            {
                BaseAddress = _httpClient.BaseAddress!,
                RefreshOnUnauthorized = false,
                UserAgent = "CottonSyncDesktop/IntegrationTest",
                DeviceName = "Cotton Sync Desktop integration test",
            });
    }

    private static Task LoginAsync(CottonCloudClient client)
    {
        return client.Auth.LoginAsync(new LoginRequestDto
        {
            Username = "testuser",
            Password = "testpassword",
        });
    }

    private static async Task<NodeFileManifestDto> UpdateRemoteTextFileAsync(
        CottonCloudClient client,
        NodeFileManifestDto existingFile,
        string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string hash = Hasher.ToHexStringHash(Hasher.HashData(bytes));
        await using var stream = new MemoryStream(bytes);
        await client.Chunks.UploadRawAsync(hash, stream, "text/plain");
        return await client.Files.UpdateContentAsync(
            existingFile.Id,
            new CreateFileFromChunksRequestDto
            {
                NodeId = existingFile.NodeId,
                ChunkHashes = [hash],
                Name = existingFile.Name,
                ContentType = "text/plain",
                Hash = hash,
                Validate = true,
            },
            existingFile.ETag);
    }

    private static SyncEngine CreateEngine(CottonCloudClient client, SqliteSyncStateStore stateStore)
    {
        return new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client, new SdkRemoteFileSynchronizerOptions { ChunkSizeBytes = 1024 }),
            stateStore);
    }

    private SqliteSyncStateStore CreateStateStore(string fileName)
    {
        return new SqliteSyncStateStore(Path.Combine(_tempDirectory, fileName));
    }

    private static async Task<string> DownloadTextAsync(CottonCloudClient client, Guid nodeFileId)
    {
        await using var downloaded = new MemoryStream();
        await client.Files.DownloadContentAsync(nodeFileId, downloaded);
        return Encoding.UTF8.GetString(downloaded.ToArray());
    }

    private static async Task<NodeFileManifestDto> FindRemoteFileAsync(
        CottonCloudClient client,
        Guid rootNodeId,
        string directoryName,
        string fileName)
    {
        NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(rootNodeId);
        NodeDto directory = rootContent.Nodes.Single(node => string.Equals(node.Name, directoryName, StringComparison.Ordinal));
        NodeContentDto directoryContent = await client.Nodes.GetChildrenAsync(directory.Id);
        return directoryContent.Files.Single(file => string.Equals(file.Name, fileName, StringComparison.Ordinal));
    }

    private static async Task<NodeFileManifestDto> CreateRemoteTextFileAsync(
        CottonCloudClient client,
        Guid parentNodeId,
        string fileName,
        string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string hash = Hasher.ToHexStringHash(Hasher.HashData(bytes));
        await using var stream = new MemoryStream(bytes);
        await client.Chunks.UploadRawAsync(hash, stream, "text/plain");
        return await client.Files.CreateFromChunksAsync(new CreateFileFromChunksRequestDto
        {
            NodeId = parentNodeId,
            ChunkHashes = [hash],
            Name = fileName,
            ContentType = "text/plain",
            Hash = hash,
            Validate = true,
        });
    }

    private static void WriteLocalFile(string localRoot, string relativePath, string content)
    {
        string fullPath = Path.Combine(localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetLastWriteTimeUtc(fullPath, new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc));
    }

    private Dictionary<string, string?> CreateServerOverrides()
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = TestPostgresHost,
            Port = TestPostgresPort,
            Database = CurrentDatabaseName,
            Username = TestPostgresUsername,
            Password = TestPostgresPassword,
        };
        return new Dictionary<string, string?>
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
            ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
        };
    }
}
