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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

[NonParallelizable]
public sealed class SyncClientEndToEndTests : IntegrationTestBase
{
    private const int SyncTestChunkSizeBytes = 1024;

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
    public async Task RunOnceAsync_MovesRemoteFileToTrashWhenLocalFileIsDeleted()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-local-delete");
        string localRoot = Path.Combine(_tempDirectory, "client-local-delete");
        const string relativePath = "Docs/delete-me.txt";
        string localFilePath = Path.Combine(localRoot, "Docs", "delete-me.txt");
        WriteLocalFile(localRoot, relativePath, "delete me");
        SqliteSyncStateStore stateStore = CreateStateStore("client-local-delete-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-local-delete",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };
        await engine.RunOnceAsync(syncPair);
        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, "Docs", "delete-me.txt");

        File.Delete(localFilePath);
        SyncRunResult result = await engine.RunOnceAsync(syncPair);

        NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
        NodeDto docs = rootContent.Nodes.Single(node => string.Equals(node.Name, "Docs", StringComparison.Ordinal));
        NodeContentDto docsContent = await client.Nodes.GetChildrenAsync(docs.Id);
        SyncStateEntry? baseline = await stateStore.GetAsync("client-local-delete", relativePath);
        DbContext.ChangeTracker.Clear();
        Guid currentNodeId = await DbContext.NodeFiles
            .AsNoTracking()
            .Where(file => file.Id == uploaded.Id)
            .Select(file => file.NodeId)
            .SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedRemote }));
            Assert.That(docsContent.Files.Select(file => file.Id), Does.Not.Contain(uploaded.Id));
            Assert.That(currentNodeId, Is.Not.EqualTo(docs.Id));
            Assert.That(baseline, Is.Null);
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

    [Test]
    public async Task RunOnceAsync_SyncsUnicodePathsThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-unicode");
        string localRoot = Path.Combine(_tempDirectory, "client-unicode");
        const string uploadPath = "Документы/設計-notes.txt";
        const string downloadPath = "共有/設計-remote.txt";
        WriteLocalFile(localRoot, uploadPath, "unicode local content");
        SqliteSyncStateStore stateStore = CreateStateStore("client-unicode-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-unicode",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };

        SyncRunResult uploadRun = await engine.RunOnceAsync(syncPair);
        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, "Документы", "設計-notes.txt");
        string uploadedContent = await DownloadTextAsync(client, uploaded.Id);
        NodeDto remoteDirectory = await client.Nodes.CreateAsync(remoteRoot.Id, "共有");
        NodeFileManifestDto remoteFile = await CreateRemoteTextFileAsync(
            client,
            remoteDirectory.Id,
            "設計-remote.txt",
            "unicode remote content");

        SyncRunResult downloadRun = await engine.RunOnceAsync(syncPair);

        string downloadedContent = File.ReadAllText(Path.Combine(localRoot, "共有", "設計-remote.txt"), Encoding.UTF8);
        SyncStateEntry? uploadedBaseline = await stateStore.GetAsync("client-unicode", uploadPath);
        SyncStateEntry? downloadedBaseline = await stateStore.GetAsync("client-unicode", downloadPath);

        Assert.Multiple(() =>
        {
            Assert.That(uploadRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(downloadRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(uploadedContent, Is.EqualTo("unicode local content"));
            Assert.That(downloadedContent, Is.EqualTo("unicode remote content"));
            Assert.That(uploadedBaseline?.RelativePath, Is.EqualTo(uploadPath));
            Assert.That(downloadedBaseline?.RelativePath, Is.EqualTo(downloadPath));
            Assert.That(downloadedBaseline?.RemoteFileId, Is.EqualTo(remoteFile.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_DoesNotUploadIgnoredTemporaryFiles()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-temp-ignore");
        string localRoot = Path.Combine(_tempDirectory, "client-temp-ignore");
        WriteLocalFile(localRoot, "Docs/keep.txt", "keep me");
        WriteLocalFile(localRoot, "upload.tmp", "tmp");
        WriteLocalFile(localRoot, "upload.temp", "temp");
        WriteLocalFile(localRoot, "download.partial", "partial");
        WriteLocalFile(localRoot, "download.part", "part");
        WriteLocalFile(localRoot, "chrome.crdownload", "browser");
        WriteLocalFile(localRoot, "browser.download", "browser");
        WriteLocalFile(localRoot, ".notes.swp", "vim");
        WriteLocalFile(localRoot, ".notes.swo", "vim");
        WriteLocalFile(localRoot, ".notes.swn", "vim");
        WriteLocalFile(localRoot, "~$office.docx", "office");
        WriteLocalFile(localRoot, ".#emacs-lock", "emacs");
        WriteLocalFile(localRoot, "backup~", "backup");
        WriteLocalFile(localRoot, ".DS_Store", "mac");
        WriteLocalFile(localRoot, "Thumbs.db", "windows");
        WriteLocalFile(localRoot, "desktop.ini", "windows");
        WriteLocalFile(localRoot, "Nested/skip.tmp", "nested tmp");
        WriteLocalFile(localRoot, ".cotton-sync/state.tmp", "metadata");
        SqliteSyncStateStore stateStore = CreateStateStore("client-temp-ignore-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);

        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "client-temp-ignore",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        });

        NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
        NodeDto docs = rootContent.Nodes.Single(node => string.Equals(node.Name, "Docs", StringComparison.Ordinal));
        NodeContentDto docsContent = await client.Nodes.GetChildrenAsync(docs.Id);
        SyncStateEntry? uploadedBaseline = await stateStore.GetAsync("client-temp-ignore", "Docs/keep.txt");
        SyncStateEntry? ignoredBaseline = await stateStore.GetAsync("client-temp-ignore", "upload.tmp");
        SyncStateEntry? nestedIgnoredBaseline = await stateStore.GetAsync("client-temp-ignore", "Nested/skip.tmp");
        SyncStateEntry? metadataBaseline = await stateStore.GetAsync("client-temp-ignore", ".cotton-sync/state.tmp");

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(rootContent.Files, Is.Empty);
            Assert.That(rootContent.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "Docs" }));
            Assert.That(docsContent.Files.Select(file => file.Name), Is.EqualTo(new[] { "keep.txt" }));
            Assert.That(uploadedBaseline, Is.Not.Null);
            Assert.That(ignoredBaseline, Is.Null);
            Assert.That(nestedIgnoredBaseline, Is.Null);
            Assert.That(metadataBaseline, Is.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_UploadsAndDownloadsLargeFilesThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-large-files");
        string localRoot = Path.Combine(_tempDirectory, "client-large-files");
        const string uploadPath = "Large/upload.bin";
        const string downloadPath = "RemoteLarge/download.bin";
        byte[] uploadBytes = CreateDeterministicBytes((SyncTestChunkSizeBytes * 5) + 333);
        byte[] remoteBytes = CreateDeterministicBytes((SyncTestChunkSizeBytes * 7) + 111);
        WriteLocalFile(localRoot, uploadPath, uploadBytes);
        SqliteSyncStateStore stateStore = CreateStateStore("client-large-files-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-large-files",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };

        SyncRunResult uploadRun = await engine.RunOnceAsync(syncPair);
        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, "Large", "upload.bin");
        byte[] uploadedRoundTrip = await DownloadBytesAsync(client, uploaded.Id);
        DbContext.ChangeTracker.Clear();
        int uploadedChunkCount = await DbContext.FileManifestChunks
            .CountAsync(chunk => chunk.FileManifestId == uploaded.FileManifestId);
        NodeDto remoteDirectory = await client.Nodes.CreateAsync(remoteRoot.Id, "RemoteLarge");
        NodeFileManifestDto remoteFile = await CreateRemoteFileAsync(
            client,
            remoteDirectory.Id,
            "download.bin",
            remoteBytes);
        DbContext.ChangeTracker.Clear();
        int remoteChunkCount = await DbContext.FileManifestChunks
            .CountAsync(chunk => chunk.FileManifestId == remoteFile.FileManifestId);

        SyncRunResult downloadRun = await engine.RunOnceAsync(syncPair);

        byte[] downloadedBytes = await File.ReadAllBytesAsync(
            Path.Combine(localRoot, "RemoteLarge", "download.bin"));
        SyncStateEntry? uploadedBaseline = await stateStore.GetAsync("client-large-files", uploadPath);
        SyncStateEntry? downloadedBaseline = await stateStore.GetAsync("client-large-files", downloadPath);

        Assert.Multiple(() =>
        {
            Assert.That(uploadRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(downloadRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(uploadedRoundTrip, Is.EqualTo(uploadBytes));
            Assert.That(downloadedBytes, Is.EqualTo(remoteBytes));
            Assert.That(uploaded.SizeBytes, Is.EqualTo(uploadBytes.Length));
            Assert.That(remoteFile.SizeBytes, Is.EqualTo(remoteBytes.Length));
            Assert.That(uploadedChunkCount, Is.GreaterThan(1));
            Assert.That(remoteChunkCount, Is.GreaterThan(1));
            Assert.That(uploadedBaseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
            Assert.That(uploadedBaseline?.RemoteContentHash, Is.EqualTo(uploaded.ContentHash));
            Assert.That(downloadedBaseline?.RemoteFileId, Is.EqualTo(remoteFile.Id));
            Assert.That(downloadedBaseline?.RemoteContentHash, Is.EqualTo(remoteFile.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_UploadsManySmallFilesThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-many-small-files");
        string localRoot = Path.Combine(_tempDirectory, "client-many-small-files");
        List<string> expectedPaths = [];
        for (int index = 0; index < 32; index++)
        {
            string relativePath = $"Batch/{index / 8:00}/file-{index:00}.txt";
            expectedPaths.Add(relativePath);
            WriteLocalFile(localRoot, relativePath, $"small file {index:00}");
        }

        SqliteSyncStateStore stateStore = CreateStateStore("client-many-small-files-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);

        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "client-many-small-files",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        });

        NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
        NodeDto batchDirectory = rootContent.Nodes.Single(node => string.Equals(node.Name, "Batch", StringComparison.Ordinal));
        NodeContentDto batchContent = await client.Nodes.GetChildrenAsync(batchDirectory.Id);
        var remoteContents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (NodeDto groupDirectory in batchContent.Nodes.OrderBy(node => node.Name, StringComparer.Ordinal))
        {
            NodeContentDto groupContent = await client.Nodes.GetChildrenAsync(groupDirectory.Id);
            foreach (NodeFileManifestDto file in groupContent.Files.OrderBy(file => file.Name, StringComparer.Ordinal))
            {
                string relativePath = $"Batch/{groupDirectory.Name}/{file.Name}";
                remoteContents[relativePath] = await DownloadTextAsync(client, file.Id);
            }
        }

        IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync("client-many-small-files");

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities, Has.Count.EqualTo(expectedPaths.Count));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.All.EqualTo(SyncActivityKind.Uploaded));
            Assert.That(remoteContents.Keys, Is.EquivalentTo(expectedPaths));
            Assert.That(remoteContents.Values, Is.EquivalentTo(Enumerable.Range(0, 32).Select(index => $"small file {index:00}")));
            Assert.That(baselines.Select(entry => entry.RelativePath), Is.EquivalentTo(expectedPaths));
        });
    }

    [Test]
    public async Task RunOnceAsync_SyncsDeepNestedPathsThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-deep-nested");
        string localRoot = Path.Combine(_tempDirectory, "client-deep-nested");
        string localDirectory = string.Join('/', Enumerable.Range(1, 10).Select(index => $"local-{index:00}"));
        string remoteDirectory = string.Join('/', Enumerable.Range(1, 10).Select(index => $"remote-{index:00}"));
        string uploadPath = $"{localDirectory}/upload-deep.txt";
        string downloadPath = $"{remoteDirectory}/download-deep.txt";
        WriteLocalFile(localRoot, uploadPath, "deep local content");
        SqliteSyncStateStore stateStore = CreateStateStore("client-deep-nested-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-deep-nested",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };

        SyncRunResult uploadRun = await engine.RunOnceAsync(syncPair);
        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, uploadPath);
        string uploadedContent = await DownloadTextAsync(client, uploaded.Id);
        Guid currentNodeId = remoteRoot.Id;
        foreach (string segment in remoteDirectory.Split('/'))
        {
            currentNodeId = (await client.Nodes.CreateAsync(currentNodeId, segment)).Id;
        }

        NodeFileManifestDto remoteFile = await CreateRemoteTextFileAsync(
            client,
            currentNodeId,
            "download-deep.txt",
            "deep remote content");

        SyncRunResult downloadRun = await engine.RunOnceAsync(syncPair);

        string downloadedContent = await File.ReadAllTextAsync(
            Path.Combine(localRoot, downloadPath.Replace('/', Path.DirectorySeparatorChar)),
            Encoding.UTF8);
        SyncStateEntry? uploadedBaseline = await stateStore.GetAsync("client-deep-nested", uploadPath);
        SyncStateEntry? downloadedBaseline = await stateStore.GetAsync("client-deep-nested", downloadPath);

        Assert.Multiple(() =>
        {
            Assert.That(uploadRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(downloadRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(uploadedContent, Is.EqualTo("deep local content"));
            Assert.That(downloadedContent, Is.EqualTo("deep remote content"));
            Assert.That(uploadedBaseline?.RelativePath, Is.EqualTo(uploadPath));
            Assert.That(uploadedBaseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
            Assert.That(downloadedBaseline?.RelativePath, Is.EqualTo(downloadPath));
            Assert.That(downloadedBaseline?.RemoteFileId, Is.EqualTo(remoteFile.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_MovesLocalFileToQuarantineWhenRemoteFileIsDeleted()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-remote-delete");
        NodeDto remoteDirectory = await client.Nodes.CreateAsync(remoteRoot.Id, "Docs");
        NodeFileManifestDto remoteFile = await CreateRemoteTextFileAsync(
            client,
            remoteDirectory.Id,
            "remote-delete.txt",
            "remote delete content");
        string localRoot = Path.Combine(_tempDirectory, "client-remote-delete");
        Directory.CreateDirectory(localRoot);
        SqliteSyncStateStore stateStore = CreateStateStore("client-remote-delete-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-remote-delete",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };
        await engine.RunOnceAsync(syncPair);

        await client.Files.DeleteAsync(remoteFile.Id);
        SyncRunResult result = await engine.RunOnceAsync(syncPair);

        string localFilePath = Path.Combine(localRoot, "Docs", "remote-delete.txt");
        string[] quarantinedFiles = Directory.GetFiles(
            Path.Combine(localRoot, ".cotton-sync", "deleted"),
            "remote-delete.txt",
            SearchOption.AllDirectories);
        SyncStateEntry? baseline = await stateStore.GetAsync("client-remote-delete", "Docs/remote-delete.txt");

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedLocal }));
            Assert.That(File.Exists(localFilePath), Is.False);
            Assert.That(quarantinedFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(quarantinedFiles[0], Encoding.UTF8), Is.EqualTo("remote delete content"));
            Assert.That(baseline, Is.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_PropagatesClientALocalChangeToClientB()
    {
        CottonCloudClient clientA = CreateClient();
        CottonCloudClient clientB = CreateClient();
        await LoginAsync(clientA);
        await LoginAsync(clientB);
        NodeDto remoteRoot = await new RemoteRootResolver(clientA.Nodes).EnsureAsync("sync-e2e-two-client");
        string localRootA = Path.Combine(_tempDirectory, "client-two-a");
        string localRootB = Path.Combine(_tempDirectory, "client-two-b");
        Directory.CreateDirectory(localRootB);
        const string relativePath = "Docs/shared.txt";
        var syncPairA = new SyncPair
        {
            SyncPairId = "client-two-a",
            LocalRootPath = localRootA,
            RemoteRootNodeId = remoteRoot.Id,
        };
        var syncPairB = new SyncPair
        {
            SyncPairId = "client-two-b",
            LocalRootPath = localRootB,
            RemoteRootNodeId = remoteRoot.Id,
        };
        SyncEngine engineA = CreateEngine(clientA, CreateStateStore("client-two-a-state.sqlite"));
        SyncEngine engineB = CreateEngine(clientB, CreateStateStore("client-two-b-state.sqlite"));

        WriteLocalFile(localRootA, relativePath, "created by client A");
        await engineA.RunOnceAsync(syncPairA);
        SyncRunResult firstClientBRun = await engineB.RunOnceAsync(syncPairB);

        WriteLocalFile(localRootA, relativePath, "updated by client A");
        await engineA.RunOnceAsync(syncPairA);
        SyncRunResult secondClientBRun = await engineB.RunOnceAsync(syncPairB);

        string clientBContent = File.ReadAllText(Path.Combine(localRootB, "Docs", "shared.txt"), Encoding.UTF8);

        Assert.Multiple(() =>
        {
            Assert.That(firstClientBRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(secondClientBRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(clientBContent, Is.EqualTo("updated by client A"));
        });
    }

    [Test]
    public async Task RunOnceAsync_CreatesConflictWhenTwoClientsEditSameFile()
    {
        CottonCloudClient clientA = CreateClient();
        CottonCloudClient clientB = CreateClient();
        await LoginAsync(clientA);
        await LoginAsync(clientB);
        NodeDto remoteRoot = await new RemoteRootResolver(clientA.Nodes).EnsureAsync("sync-e2e-two-client-conflict");
        string localRootA = Path.Combine(_tempDirectory, "client-conflict-a");
        string localRootB = Path.Combine(_tempDirectory, "client-conflict-b");
        Directory.CreateDirectory(localRootB);
        const string relativePath = "Docs/conflict.txt";
        var syncPairA = new SyncPair
        {
            SyncPairId = "client-conflict-a",
            LocalRootPath = localRootA,
            RemoteRootNodeId = remoteRoot.Id,
        };
        var syncPairB = new SyncPair
        {
            SyncPairId = "client-conflict-b",
            LocalRootPath = localRootB,
            RemoteRootNodeId = remoteRoot.Id,
        };
        SqliteSyncStateStore stateStoreB = CreateStateStore("client-conflict-b-state.sqlite");
        SyncEngine engineA = CreateEngine(clientA, CreateStateStore("client-conflict-a-state.sqlite"));
        SyncEngine engineB = CreateEngine(clientB, stateStoreB);
        WriteLocalFile(localRootA, relativePath, "initial content");
        await engineA.RunOnceAsync(syncPairA);
        await engineB.RunOnceAsync(syncPairB);

        WriteLocalFile(localRootA, relativePath, "client A content");
        await engineA.RunOnceAsync(syncPairA);
        WriteLocalFile(localRootB, relativePath, "client B content");
        SyncRunResult conflictRun = await engineB.RunOnceAsync(syncPairB);

        string clientBMainContent = File.ReadAllText(Path.Combine(localRootB, "Docs", "conflict.txt"), Encoding.UTF8);
        string[] conflictFiles = Directory.GetFiles(localRootB, "*Cotton conflict*", SearchOption.AllDirectories);
        SyncStateEntry? baseline = await stateStoreB.GetAsync("client-conflict-b", relativePath);

        Assert.Multiple(() =>
        {
            Assert.That(conflictRun.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Conflict }));
            Assert.That(conflictRun.Activities.Single().Details, Does.Contain("Cotton conflict"));
            Assert.That(clientBMainContent, Is.EqualTo("client B content"));
            Assert.That(conflictFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(conflictFiles[0], Encoding.UTF8), Is.EqualTo("client A content"));
            Assert.That(baseline?.LocalContentHash, Is.Not.EqualTo(baseline?.RemoteContentHash));
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
            new SdkRemoteFileSynchronizer(client, new SdkRemoteFileSynchronizerOptions { ChunkSizeBytes = SyncTestChunkSizeBytes }),
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

    private static async Task<byte[]> DownloadBytesAsync(CottonCloudClient client, Guid nodeFileId)
    {
        await using var downloaded = new MemoryStream();
        await client.Files.DownloadContentAsync(nodeFileId, downloaded);
        return downloaded.ToArray();
    }

    private static async Task<NodeFileManifestDto> FindRemoteFileAsync(
        CottonCloudClient client,
        Guid rootNodeId,
        string directoryName,
        string fileName)
    {
        return await FindRemoteFileAsync(client, rootNodeId, $"{directoryName}/{fileName}");
    }

    private static async Task<NodeFileManifestDto> FindRemoteFileAsync(
        CottonCloudClient client,
        Guid rootNodeId,
        string relativePath)
    {
        string normalized = SyncPath.Normalize(relativePath);
        string[] segments = normalized.Split('/');
        Guid nodeId = rootNodeId;
        for (int index = 0; index < segments.Length - 1; index++)
        {
            NodeContentDto content = await client.Nodes.GetChildrenAsync(nodeId);
            nodeId = content.Nodes.Single(node => string.Equals(node.Name, segments[index], StringComparison.Ordinal)).Id;
        }

        NodeContentDto directoryContent = await client.Nodes.GetChildrenAsync(nodeId);
        return directoryContent.Files.Single(file => string.Equals(file.Name, segments[^1], StringComparison.Ordinal));
    }

    private static async Task<NodeFileManifestDto> CreateRemoteTextFileAsync(
        CottonCloudClient client,
        Guid parentNodeId,
        string fileName,
        string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return await CreateRemoteFileAsync(client, parentNodeId, fileName, bytes, "text/plain");
    }

    private static async Task<NodeFileManifestDto> CreateRemoteFileAsync(
        CottonCloudClient client,
        Guid parentNodeId,
        string fileName,
        byte[] bytes,
        string contentType = "application/octet-stream")
    {
        string hash = Hasher.ToHexStringHash(Hasher.HashData(bytes));
        IReadOnlyList<string> chunkHashes = await UploadContentChunksAsync(client, bytes, contentType);
        return await client.Files.CreateFromChunksAsync(new CreateFileFromChunksRequestDto
        {
            NodeId = parentNodeId,
            ChunkHashes = chunkHashes.ToList(),
            Name = fileName,
            ContentType = contentType,
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

    private static void WriteLocalFile(string localRoot, string relativePath, byte[] content)
    {
        string fullPath = Path.Combine(localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        File.SetLastWriteTimeUtc(fullPath, new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc));
    }

    private static async Task<IReadOnlyList<string>> UploadContentChunksAsync(
        CottonCloudClient client,
        byte[] content,
        string contentType)
    {
        var chunkHashes = new List<string>();
        for (int offset = 0; offset < content.Length; offset += SyncTestChunkSizeBytes)
        {
            int count = Math.Min(SyncTestChunkSizeBytes, content.Length - offset);
            byte[] chunk = content.AsSpan(offset, count).ToArray();
            string chunkHash = Hasher.ToHexStringHash(Hasher.HashData(chunk));
            await using var stream = new MemoryStream(chunk);
            await client.Chunks.UploadRawAsync(chunkHash, stream, contentType);
            chunkHashes.Add(chunkHash);
        }

        if (chunkHashes.Count == 0)
        {
            string emptyHash = Hasher.ToHexStringHash(Hasher.HashData(Array.Empty<byte>()));
            await using var stream = new MemoryStream();
            await client.Chunks.UploadRawAsync(emptyHash, stream, contentType);
            chunkHashes.Add(emptyHash);
        }

        return chunkHashes;
    }

    private static byte[] CreateDeterministicBytes(int length)
    {
        var bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)((index * 31 + index / 7) % 251);
        }

        return bytes;
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
