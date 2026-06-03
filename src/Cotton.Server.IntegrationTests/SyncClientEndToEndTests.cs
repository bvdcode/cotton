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
        await client.Auth.LoginAsync(new LoginRequestDto
        {
            Username = "testuser",
            Password = "testpassword",
        });
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e");
        string localRoot = Path.Combine(_tempDirectory, "client-a");
        WriteLocalFile(localRoot, "Docs/hello.txt", "hello from sync core");
        var stateStore = new SqliteSyncStateStore(Path.Combine(_tempDirectory, "client-a-state.sqlite"));
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client, new SdkRemoteFileSynchronizerOptions { ChunkSizeBytes = 1024 }),
            stateStore);

        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "client-a",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        });

        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, "Docs", "hello.txt");
        await using var downloaded = new MemoryStream();
        await client.Files.DownloadContentAsync(uploaded.Id, downloaded);
        SyncStateEntry? baseline = await stateStore.GetAsync("client-a", "Docs/hello.txt");

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(Encoding.UTF8.GetString(downloaded.ToArray()), Is.EqualTo("hello from sync core"));
            Assert.That(uploaded.ContentHash, Is.EqualTo(baseline?.RemoteContentHash));
            Assert.That(baseline?.LocalContentHash, Is.EqualTo(uploaded.ContentHash));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
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
