// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
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
using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Cli;
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
    public async Task RunOnceAsync_ReportsUploadProgressThroughSdkToServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-progress");
        string localRoot = Path.Combine(_tempDirectory, "client-progress");
        const string relativePath = "Docs/progress.bin";
        byte[] content = CreateDeterministicBytes((SyncTestChunkSizeBytes * 2) + 123);
        WriteLocalFile(localRoot, relativePath, content);
        SqliteSyncStateStore stateStore = CreateStateStore("client-progress-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var transferProgress = new RecordingProgress<SyncTransferProgress>();
        var runProgress = new RecordingProgress<SyncRunProgress>();
        var activityProgress = new RecordingProgress<SyncActivity>();

        SyncRunResult result = await engine.RunOnceAsync(
            new SyncPair
            {
                SyncPairId = "client-progress",
                LocalRootPath = localRoot,
                RemoteRootNodeId = remoteRoot.Id,
            },
            new SyncRunOptions
            {
                ActivityProgress = activityProgress,
                TransferProgress = transferProgress,
                RunProgress = runProgress,
            });

        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, relativePath);
        byte[] roundTrip = await DownloadBytesAsync(client, uploaded.Id);
        IReadOnlyList<long> transferredBytes = transferProgress.Values.Select(progress => progress.TransferredBytes).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(activityProgress.Values.Select(activity => activity.RelativePath), Is.EqualTo(new[] { relativePath }));
            Assert.That(runProgress.Values.Any(progress => progress.Stage == SyncRunProgressStage.ReconcilingFiles && progress.CurrentPath == relativePath), Is.True);
            Assert.That(runProgress.Values[^1].Stage, Is.EqualTo(SyncRunProgressStage.Completed));
            Assert.That(runProgress.Values[^1].IsCompleted, Is.True);
            Assert.That(transferProgress.Values.Select(progress => progress.Direction), Is.All.EqualTo(SyncTransferDirection.Upload));
            Assert.That(transferProgress.Values.Select(progress => progress.RelativePath), Is.All.EqualTo(relativePath));
            Assert.That(transferProgress.Values.Select(progress => progress.TotalBytes), Is.All.EqualTo(content.Length));
            Assert.That(transferredBytes, Is.EqualTo(new long[] { 0, SyncTestChunkSizeBytes, SyncTestChunkSizeBytes * 2, content.Length, content.Length }));
            Assert.That(transferProgress.Values[^1].IsCompleted, Is.True);
            Assert.That(uploaded.SizeBytes, Is.EqualTo(content.Length));
            Assert.That(roundTrip, Is.EqualTo(content));
        });
    }

    [Test]
    public async Task SyncCliCommandRunner_SyncOnceUploadsLocalFileThroughSdkToServer()
    {
        Assert.That(_httpClient, Is.Not.Null);
        Assert.That(_httpClient!.BaseAddress, Is.Not.Null);
        Assert.That(_factory, Is.Not.Null);
        CottonCloudClient setupClient = CreateClient();
        await LoginAsync(setupClient);
        NodeDto remoteRoot = await new RemoteRootResolver(setupClient.Nodes).EnsureAsync("sync-cli-e2e");
        string localRoot = Path.Combine(_tempDirectory, "cli-client");
        const string relativePath = "Cli/report.txt";
        WriteLocalFile(localRoot, relativePath, "hello from sync cli");
        string databasePath = Path.Combine(_tempDirectory, "cli-sync-state.sqlite");
        using HttpClient cliHttpClient = _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            [
                "sync-once",
                "--server",
                cliHttpClient.BaseAddress!.ToString(),
                "--username",
                "testuser",
                "--password",
                "testpassword",
                "--local-root",
                localRoot,
                "--remote-root",
                remoteRoot.Id.ToString("D"),
                "--sync-pair",
                "sync-cli-e2e",
                "--database",
                databasePath,
            ],
            output,
            error,
            cliHttpClient);

        NodeFileManifestDto uploaded = await FindRemoteFileAsync(setupClient, remoteRoot.Id, relativePath);
        string downloaded = await DownloadTextAsync(setupClient, uploaded.Id);
        var stateStore = new SqliteSyncStateStore(databasePath);
        await stateStore.InitializeAsync();
        SyncStateEntry? baseline = await stateStore.GetAsync("sync-cli-e2e", relativePath);
        SyncStateEntry? directoryBaseline = await stateStore.GetAsync("sync-cli-e2e", "Cli");
        string outputText = output.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(outputText, Does.Contain("Cotton Sync one-shot run"));
            Assert.That(outputText, Does.Contain("Activities: 2"));
            Assert.That(outputText, Does.Contain("Uploaded Cli - Created remote folder."));
            Assert.That(outputText, Does.Contain("Uploaded Cli/report.txt"));
            Assert.That(outputText, Does.Contain("State entries: 2"));
            Assert.That(downloaded, Is.EqualTo("hello from sync cli"));
            Assert.That(directoryBaseline?.Kind, Is.EqualTo(SyncEntryKind.Directory));
            Assert.That(directoryBaseline?.RemoteNodeId, Is.Not.Null);
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(uploaded.ContentHash));
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
            Assert.That(
                GetActivityPaths(result, SyncActivityKind.Downloaded),
                Is.EquivalentTo(new[] { "Docs", "Docs/remote.txt" }));
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
            Assert.That(
                GetActivityPaths(result, SyncActivityKind.Downloaded),
                Is.EquivalentTo(new[] { "Docs/remote-update.txt" }));
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
            Assert.That(
                GetActivityPaths(downloadRun, SyncActivityKind.Downloaded),
                Is.EquivalentTo(new[] { "共有", downloadPath }));
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
            Assert.That(
                GetActivityPaths(downloadRun, SyncActivityKind.Downloaded),
                Is.EquivalentTo(new[] { "RemoteLarge", downloadPath }));
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
            Assert.That(
                GetActivityPaths(downloadRun, SyncActivityKind.Downloaded),
                Is.EquivalentTo(GetDirectoryPrefixes(remoteDirectory).Append(downloadPath)));
            Assert.That(uploadedContent, Is.EqualTo("deep local content"));
            Assert.That(downloadedContent, Is.EqualTo("deep remote content"));
            Assert.That(uploadedBaseline?.RelativePath, Is.EqualTo(uploadPath));
            Assert.That(uploadedBaseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
            Assert.That(downloadedBaseline?.RelativePath, Is.EqualTo(downloadPath));
            Assert.That(downloadedBaseline?.RemoteFileId, Is.EqualTo(remoteFile.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_RejectsLocalCaseInsensitivePathCollisionThroughSdkAndServer()
    {
        if (!IsCaseSensitiveFileSystem(_tempDirectory))
        {
            Assert.Ignore("This case-conflict smoke requires a case-sensitive local filesystem.");
        }

        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-case-conflict");
        string localRoot = Path.Combine(_tempDirectory, "client-case-conflict");
        WriteLocalFile(localRoot, "Case/File.txt", "first");
        WriteLocalFile(localRoot, "case/file.txt", "second");
        SqliteSyncStateStore stateStore = CreateStateStore("client-case-conflict-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);

        SyncPathCollisionException? exception = Assert.ThrowsAsync<SyncPathCollisionException>(() => engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "client-case-conflict",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        }));

        NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
        IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync("client-case-conflict");

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(new[] { exception!.FirstPath, exception.SecondPath }, Is.EquivalentTo(new[] { "Case", "case" }));
            Assert.That(exception.Message, Does.Contain("Case-insensitive path collision"));
            Assert.That(rootContent.Nodes, Is.Empty);
            Assert.That(rootContent.Files, Is.Empty);
            Assert.That(baselines, Is.Empty);
        });
    }

    [Test]
    public async Task RunOnceAsync_RejectsWindowsReservedLocalFileNameThroughSdkAndServer()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows blocks reserved device filenames before the sync scanner can validate them.");
        }

        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-windows-reserved-name");
        string localRoot = Path.Combine(_tempDirectory, "client-windows-reserved-name");
        WriteLocalFile(localRoot, "Docs/CON.txt", "reserved device name");
        SqliteSyncStateStore stateStore = CreateStateStore("client-windows-reserved-name-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);

        SyncPathValidationException? exception = Assert.ThrowsAsync<SyncPathValidationException>(() => engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "client-windows-reserved-name",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        }));

        NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
        IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync("client-windows-reserved-name");

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.RelativePath, Is.EqualTo("Docs/CON.txt"));
            Assert.That(exception.Segment, Is.EqualTo("CON.txt"));
            Assert.That(exception.Reason, Does.Contain("device name 'CON'"));
            Assert.That(rootContent.Nodes, Is.Empty);
            Assert.That(rootContent.Files, Is.Empty);
            Assert.That(baselines, Is.Empty);
        });
    }

    [Test]
    public async Task SyncPairRunner_UploadsLockedFileAfterItBecomesReadableThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-locked-file-retry");
        string localRoot = Path.Combine(_tempDirectory, "client-locked-file-retry");
        WriteLocalFile(localRoot, "locked.txt", "locked content");
        FileStream? locked = new(
            Path.Combine(localRoot, "locked.txt"),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        SqliteSyncStateStore stateStore = CreateStateStore("client-locked-file-retry-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Locked file retry",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
            RemoteDisplayPath = "/Locked file retry",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var work = new ReleaseLockAfterFirstUnavailableSyncPairWork(
            new SyncEnginePairWork(engine),
            () =>
            {
                locked?.Dispose();
                locked = null;
            });
        var runner = new SyncPairRunner(syncPair, work, CreateNoDelayRetryOptions());

        try
        {
            await runner.SyncNowAsync();

            NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, "locked.txt");
            string uploadedContent = await DownloadTextAsync(client, uploaded.Id);
            SyncStateEntry? baseline = await stateStore.GetAsync(syncPair.Id.ToString("D"), "locked.txt");

            Assert.Multiple(() =>
            {
                Assert.That(work.RunCount, Is.EqualTo(2));
                Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
                Assert.That(uploadedContent, Is.EqualTo("locked content"));
                Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
                Assert.That(baseline?.RemoteContentHash, Is.EqualTo(uploaded.ContentHash));
            });
        }
        finally
        {
            locked?.Dispose();
        }
    }

    [Test]
    public async Task SyncPairRunner_ReportsQuotaExceededThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        TokenPairDto tokens = await LoginAsync(client);
        Assert.That(_httpClient, Is.Not.Null);
        _httpClient!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        try
        {
            using HttpResponseMessage quotaResponse = await _httpClient.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes",
                5L);
            quotaResponse.EnsureSuccessStatusCode();
            NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-quota-exceeded");
            string localRoot = Path.Combine(_tempDirectory, "client-quota-exceeded");
            WriteLocalFile(localRoot, "too-large.txt", "abcdef");
            SqliteSyncStateStore stateStore = CreateStateStore("client-quota-exceeded-state.sqlite");
            SyncEngine engine = CreateEngine(client, stateStore);
            var syncPair = new SyncPairSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = "Quota exceeded",
                LocalRootPath = localRoot,
                RemoteRootNodeId = remoteRoot.Id,
                RemoteDisplayPath = "/Quota exceeded",
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            var runner = new SyncPairRunner(
                syncPair,
                new SyncEnginePairWork(engine),
                CreateNoDelayRetryOptions(maxAttempts: 1));

            CottonApiException? exception = Assert.ThrowsAsync<CottonApiException>(() => runner.SyncNowAsync());

            NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
            IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync(syncPair.Id.ToString("D"));
            const string expected = "Remote storage quota exceeded. Free space in Cotton Cloud or choose a smaller sync folder.";

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.StatusCode, Is.EqualTo((System.Net.HttpStatusCode)507));
                Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Error));
                Assert.That(runner.Status.LastError, Is.EqualTo(expected));
                Assert.That(runner.Status.CurrentOperation, Is.EqualTo("Action required: " + expected));
                Assert.That(rootContent.Files, Is.Empty);
                Assert.That(baselines, Is.Empty);
            });
        }
        finally
        {
            using HttpResponseMessage resetQuotaResponse = await _httpClient.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes",
                0L);
            resetQuotaResponse.EnsureSuccessStatusCode();
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    [Test]
    public async Task SyncPairRunner_ReportsPermissionDeniedForUnreadableLocalFileThroughSdkAndServer()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows permission-denied coverage requires an ACL-specific test on Windows.");
            return;
        }

        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-permission-denied");
        string localRoot = Path.Combine(_tempDirectory, "client-permission-denied");
        const string relativePath = "Private/secret.txt";
        WriteLocalFile(localRoot, relativePath, "secret");
        string filePath = Path.Combine(localRoot, "Private", "secret.txt");
        UnixFileMode originalMode = File.GetUnixFileMode(filePath);
        SqliteSyncStateStore stateStore = CreateStateStore("client-permission-denied-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Permission denied",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
            RemoteDisplayPath = "/Permission denied",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var runner = new SyncPairRunner(
            syncPair,
            new SyncEnginePairWork(engine),
            CreateNoDelayRetryOptions(maxAttempts: 1));

        try
        {
            File.SetUnixFileMode(filePath, UnixFileMode.None);

            LocalFilePermissionDeniedException? exception = Assert.ThrowsAsync<LocalFilePermissionDeniedException>(() => runner.SyncNowAsync());

            NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
            IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync(syncPair.Id.ToString("D"));
            const string expected = "Permission denied while accessing local sync files. Check folder permissions and retry.";

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.RelativePath, Is.EqualTo(relativePath));
                Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Error));
                Assert.That(runner.Status.LastError, Is.EqualTo(expected));
                Assert.That(runner.Status.CurrentOperation, Is.EqualTo("Action required: " + expected));
                Assert.That(rootContent.Nodes, Is.Empty);
                Assert.That(rootContent.Files, Is.Empty);
                Assert.That(baselines, Is.Empty);
            });
        }
        finally
        {
            File.SetUnixFileMode(filePath, originalMode | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Test]
    public async Task SyncPairRunner_ReportsDiskFullDuringDownloadThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-disk-full");
        NodeDto remoteDirectory = await client.Nodes.CreateAsync(remoteRoot.Id, "DiskFull");
        NodeFileManifestDto remoteFile = await CreateRemoteTextFileAsync(
            client,
            remoteDirectory.Id,
            "remote-download.txt",
            "cannot fit locally");
        string localRoot = Path.Combine(_tempDirectory, "client-disk-full");
        Directory.CreateDirectory(localRoot);
        SqliteSyncStateStore stateStore = CreateStateStore("client-disk-full-state.sqlite");
        var localWriter = new DiskFullLocalFileSyncWriter();
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client, new SdkRemoteFileSynchronizerOptions { ChunkSizeBytes = SyncTestChunkSizeBytes }),
            stateStore,
            localWriter);
        var syncPair = new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Disk full",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
            RemoteDisplayPath = "/Disk full",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var runner = new SyncPairRunner(
            syncPair,
            new SyncEnginePairWork(engine),
            CreateNoDelayRetryOptions(maxAttempts: 1));

        TestDiskFullIOException? exception = Assert.ThrowsAsync<TestDiskFullIOException>(() => runner.SyncNowAsync());

        string localFilePath = Path.Combine(localRoot, "DiskFull", "remote-download.txt");
        IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync(syncPair.Id.ToString("D"));
        const string expected = "Local disk is full. Free space on this computer and retry sync.";

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(localWriter.WriteAttempts, Is.EqualTo(1));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Error));
            Assert.That(runner.Status.LastError, Is.EqualTo(expected));
            Assert.That(runner.Status.CurrentOperation, Is.EqualTo("Action required: " + expected));
            Assert.That(File.Exists(localFilePath), Is.False);
            Assert.That(
                baselines.Where(entry => entry.Kind == SyncEntryKind.Directory).Select(entry => entry.RelativePath),
                Is.EquivalentTo(new[] { "DiskFull" }));
            Assert.That(baselines.Where(entry => entry.Kind == SyncEntryKind.File), Is.Empty);
            Assert.That(remoteFile.Name, Is.EqualTo("remote-download.txt"));
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
            Assert.That(GetActivityPaths(firstClientBRun, SyncActivityKind.Downloaded), Is.EquivalentTo(new[] { "Docs", "Docs/shared.txt" }));
            Assert.That(GetActivityPaths(secondClientBRun, SyncActivityKind.Downloaded), Is.EquivalentTo(new[] { "Docs/shared.txt" }));
            Assert.That(clientBContent, Is.EqualTo("updated by client A"));
        });
    }

    [Test]
    public async Task RunOnceAsync_PropagatesClientARenameMoveToClientB()
    {
        CottonCloudClient clientA = CreateClient();
        CottonCloudClient clientB = CreateClient();
        await LoginAsync(clientA);
        await LoginAsync(clientB);
        NodeDto remoteRoot = await new RemoteRootResolver(clientA.Nodes).EnsureAsync("sync-e2e-two-client-move");
        string localRootA = Path.Combine(_tempDirectory, "client-move-a");
        string localRootB = Path.Combine(_tempDirectory, "client-move-b");
        Directory.CreateDirectory(localRootB);
        const string initialPath = "Docs/draft.txt";
        const string movedPath = "Archive/Reports/final.txt";
        var syncPairA = new SyncPair
        {
            SyncPairId = "client-move-a",
            LocalRootPath = localRootA,
            RemoteRootNodeId = remoteRoot.Id,
        };
        var syncPairB = new SyncPair
        {
            SyncPairId = "client-move-b",
            LocalRootPath = localRootB,
            RemoteRootNodeId = remoteRoot.Id,
        };
        SqliteSyncStateStore stateStoreB = CreateStateStore("client-move-b-state.sqlite");
        SyncEngine engineA = CreateEngine(clientA, CreateStateStore("client-move-a-state.sqlite"));
        SyncEngine engineB = CreateEngine(clientB, stateStoreB);
        WriteLocalFile(localRootA, initialPath, "moved by client A");
        await engineA.RunOnceAsync(syncPairA);
        await engineB.RunOnceAsync(syncPairB);

        string sourcePath = Path.Combine(localRootA, "Docs", "draft.txt");
        string targetPath = Path.Combine(localRootA, "Archive", "Reports", "final.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Move(sourcePath, targetPath);
        SyncRunResult clientARun = await engineA.RunOnceAsync(syncPairA);
        SyncRunResult clientBRun = await engineB.RunOnceAsync(syncPairB);

        NodeFileManifestDto movedRemote = await FindRemoteFileAsync(clientA, remoteRoot.Id, movedPath);
        string movedRemoteContent = await DownloadTextAsync(clientA, movedRemote.Id);
        NodeContentDto rootContent = await clientA.Nodes.GetChildrenAsync(remoteRoot.Id);
        NodeDto docs = rootContent.Nodes.Single(node => string.Equals(node.Name, "Docs", StringComparison.Ordinal));
        NodeContentDto docsContent = await clientA.Nodes.GetChildrenAsync(docs.Id);
        string movedClientBContent = File.ReadAllText(Path.Combine(localRootB, "Archive", "Reports", "final.txt"), Encoding.UTF8);
        string[] quarantinedFiles = Directory.GetFiles(
            Path.Combine(localRootB, ".cotton-sync", "deleted"),
            "draft.txt",
            SearchOption.AllDirectories);
        SyncStateEntry? oldBaseline = await stateStoreB.GetAsync("client-move-b", initialPath);
        SyncStateEntry? movedBaseline = await stateStoreB.GetAsync("client-move-b", movedPath);

        Assert.Multiple(() =>
        {
            Assert.That(clientARun.Activities.Select(activity => activity.Kind), Is.EquivalentTo(new[] { SyncActivityKind.Uploaded, SyncActivityKind.DeletedRemote }));
            Assert.That(GetActivityPaths(clientBRun, SyncActivityKind.Downloaded), Is.EquivalentTo(new[] { "Archive", "Archive/Reports", movedPath }));
            Assert.That(GetActivityPaths(clientBRun, SyncActivityKind.DeletedLocal), Is.EquivalentTo(new[] { initialPath }));
            Assert.That(File.Exists(Path.Combine(localRootB, "Docs", "draft.txt")), Is.False);
            Assert.That(movedClientBContent, Is.EqualTo("moved by client A"));
            Assert.That(movedRemoteContent, Is.EqualTo("moved by client A"));
            Assert.That(docsContent.Files.Select(file => file.Name), Does.Not.Contain("draft.txt"));
            Assert.That(quarantinedFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(quarantinedFiles[0], Encoding.UTF8), Is.EqualTo("moved by client A"));
            Assert.That(oldBaseline, Is.Null);
            Assert.That(movedBaseline?.RemoteFileId, Is.EqualTo(movedRemote.Id));
            Assert.That(movedBaseline?.RemoteContentHash, Is.EqualTo(movedRemote.ContentHash));
        });
    }

    [Test]
    public async Task SyncPairRunner_UploadsLocalChangesAfterOfflineRecoveryThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-offline-local");
        string localRoot = Path.Combine(_tempDirectory, "client-offline-local");
        const string relativePath = "Offline/local-created.txt";
        WriteLocalFile(localRoot, relativePath, "created while offline");
        SqliteSyncStateStore stateStore = CreateStateStore("client-offline-local-state.sqlite");
        await stateStore.InitializeAsync();
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Offline local recovery",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
            RemoteDisplayPath = "/Offline local recovery",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var work = new FailOnceBeforeDelegatingSyncPairWork(
            new SyncEnginePairWork(engine),
            new HttpRequestException("server unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));
        var runner = new SyncPairRunner(syncPair, work, CreateNoDelayRetryOptions(maxAttempts: 1));

        HttpRequestException? offlineException = Assert.ThrowsAsync<HttpRequestException>(() => runner.SyncNowAsync());
        SyncPairStatus offlineStatus = runner.Status;
        NodeContentDto remoteContentWhileOffline = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
        IReadOnlyList<SyncStateEntry> baselinesWhileOffline = await stateStore.LoadPairAsync(syncPair.Id.ToString("D"));

        await runner.SyncNowAsync();

        NodeFileManifestDto uploaded = await FindRemoteFileAsync(client, remoteRoot.Id, relativePath);
        string uploadedContent = await DownloadTextAsync(client, uploaded.Id);
        SyncStateEntry? baseline = await stateStore.GetAsync(syncPair.Id.ToString("D"), relativePath);

        Assert.Multiple(() =>
        {
            Assert.That(offlineException, Is.Not.Null);
            Assert.That(offlineStatus.State, Is.EqualTo(SyncPairRunState.Offline));
            Assert.That(offlineStatus.CurrentOperation, Is.EqualTo("Waiting for connection: server unavailable"));
            Assert.That(remoteContentWhileOffline.Nodes, Is.Empty);
            Assert.That(remoteContentWhileOffline.Files, Is.Empty);
            Assert.That(baselinesWhileOffline, Is.Empty);
            Assert.That(work.RunCount, Is.EqualTo(2));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
            Assert.That(uploadedContent, Is.EqualTo("created while offline"));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploaded.Id));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(uploaded.ContentHash));
        });
    }

    [Test]
    public async Task SyncPairRunner_DownloadsRemoteChangesAfterOfflineRecoveryThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-offline-remote");
        NodeDto remoteDirectory = await client.Nodes.CreateAsync(remoteRoot.Id, "RemoteOffline");
        NodeFileManifestDto remoteFile = await CreateRemoteTextFileAsync(
            client,
            remoteDirectory.Id,
            "remote-created.txt",
            "created remotely while offline");
        string localRoot = Path.Combine(_tempDirectory, "client-offline-remote");
        Directory.CreateDirectory(localRoot);
        const string relativePath = "RemoteOffline/remote-created.txt";
        SqliteSyncStateStore stateStore = CreateStateStore("client-offline-remote-state.sqlite");
        await stateStore.InitializeAsync();
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Offline remote recovery",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
            RemoteDisplayPath = "/Offline remote recovery",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var work = new FailOnceBeforeDelegatingSyncPairWork(
            new SyncEnginePairWork(engine),
            new HttpRequestException("server unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));
        var runner = new SyncPairRunner(syncPair, work, CreateNoDelayRetryOptions(maxAttempts: 1));

        HttpRequestException? offlineException = Assert.ThrowsAsync<HttpRequestException>(() => runner.SyncNowAsync());
        SyncPairStatus offlineStatus = runner.Status;
        string localFilePath = Path.Combine(localRoot, "RemoteOffline", "remote-created.txt");
        IReadOnlyList<SyncStateEntry> baselinesWhileOffline = await stateStore.LoadPairAsync(syncPair.Id.ToString("D"));

        await runner.SyncNowAsync();

        string downloadedContent = File.ReadAllText(localFilePath, Encoding.UTF8);
        SyncStateEntry? baseline = await stateStore.GetAsync(syncPair.Id.ToString("D"), relativePath);

        Assert.Multiple(() =>
        {
            Assert.That(offlineException, Is.Not.Null);
            Assert.That(offlineStatus.State, Is.EqualTo(SyncPairRunState.Offline));
            Assert.That(offlineStatus.CurrentOperation, Is.EqualTo("Waiting for connection: server unavailable"));
            Assert.That(baselinesWhileOffline, Is.Empty);
            Assert.That(work.RunCount, Is.EqualTo(2));
            Assert.That(runner.Status.State, Is.EqualTo(SyncPairRunState.Idle));
            Assert.That(File.Exists(localFilePath), Is.True);
            Assert.That(downloadedContent, Is.EqualTo("created remotely while offline"));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(remoteFile.Id));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(remoteFile.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_RecoversAfterServerHostRestartWithExistingLocalState()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-server-restart");
        string localRoot = Path.Combine(_tempDirectory, "client-server-restart");
        const string relativePath = "Restart/restarted.txt";
        WriteLocalFile(localRoot, relativePath, "before server restart");
        SqliteSyncStateStore stateStore = CreateStateStore("client-server-restart-state.sqlite");
        SyncEngine engine = CreateEngine(client, stateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-server-restart",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };
        await engine.RunOnceAsync(syncPair);
        NodeFileManifestDto uploadedBeforeRestart = await FindRemoteFileAsync(client, remoteRoot.Id, relativePath);

        RestartServerHost();
        CottonCloudClient restartedClient = CreateClient();
        await LoginAsync(restartedClient);
        WriteLocalFile(localRoot, relativePath, "after server restart");
        SyncEngine restartedEngine = CreateEngine(restartedClient, stateStore);
        SyncRunResult result = await restartedEngine.RunOnceAsync(syncPair);

        NodeFileManifestDto uploadedAfterRestart = await FindRemoteFileAsync(restartedClient, remoteRoot.Id, relativePath);
        string remoteContent = await DownloadTextAsync(restartedClient, uploadedAfterRestart.Id);
        SyncStateEntry? baseline = await stateStore.GetAsync(syncPair.SyncPairId, relativePath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(uploadedAfterRestart.Id, Is.EqualTo(uploadedBeforeRestart.Id));
            Assert.That(uploadedAfterRestart.ContentHash, Is.Not.EqualTo(uploadedBeforeRestart.ContentHash));
            Assert.That(remoteContent, Is.EqualTo("after server restart"));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploadedAfterRestart.Id));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(uploadedAfterRestart.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_RecoversAfterServerHostRestartDuringUploadBeforeBaselineUpdate()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-server-restart-during-upload");
        string localRoot = Path.Combine(_tempDirectory, "client-server-restart-during-upload");
        const string relativePath = "Restart/restarted-during-upload.txt";
        WriteLocalFile(localRoot, relativePath, "uploaded before server restart");
        SqliteSyncStateStore stateStore = CreateStateStore("client-server-restart-during-upload-state.sqlite");
        var syncPair = new SyncPair
        {
            SyncPairId = "client-server-restart-during-upload",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };
        var restartingRemoteFiles = new RestartAfterUploadRemoteFileSynchronizer(
            new SdkRemoteFileSynchronizer(client, new SdkRemoteFileSynchronizerOptions { ChunkSizeBytes = SyncTestChunkSizeBytes }),
            RestartServerHost);
        SyncEngine interruptedEngine = CreateEngine(client, stateStore, restartingRemoteFiles);

        HttpRequestException? restartException = Assert.ThrowsAsync<HttpRequestException>(() => interruptedEngine.RunOnceAsync(syncPair));

        CottonCloudClient restartedClient = CreateClient();
        await LoginAsync(restartedClient);
        NodeFileManifestDto uploadedBeforeRecovery = await FindRemoteFileAsync(restartedClient, remoteRoot.Id, relativePath);
        string uploadedContent = await DownloadTextAsync(restartedClient, uploadedBeforeRecovery.Id);
        IReadOnlyList<SyncStateEntry> baselinesAfterInterruptedRun = await stateStore.LoadPairAsync(syncPair.SyncPairId);

        SyncEngine recoveredEngine = CreateEngine(restartedClient, stateStore);
        SyncRunResult recoveryResult = await recoveredEngine.RunOnceAsync(syncPair);

        NodeFileManifestDto uploadedAfterRecovery = await FindRemoteFileAsync(restartedClient, remoteRoot.Id, relativePath);
        NodeContentDto rootContent = await restartedClient.Nodes.GetChildrenAsync(remoteRoot.Id);
        NodeDto restartDirectory = rootContent.Nodes.Single(node => string.Equals(node.Name, "Restart", StringComparison.Ordinal));
        NodeContentDto restartDirectoryContent = await restartedClient.Nodes.GetChildrenAsync(restartDirectory.Id);
        SyncStateEntry? baseline = await stateStore.GetAsync(syncPair.SyncPairId, relativePath);

        Assert.Multiple(() =>
        {
            Assert.That(restartException, Is.Not.Null);
            Assert.That(restartException!.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.ServiceUnavailable));
            Assert.That(restartingRemoteFiles.UploadAttempts, Is.EqualTo(1));
            Assert.That(uploadedContent, Is.EqualTo("uploaded before server restart"));
            Assert.That(baselinesAfterInterruptedRun, Is.Empty);
            Assert.That(recoveryResult.Activities, Is.Empty);
            Assert.That(uploadedAfterRecovery.Id, Is.EqualTo(uploadedBeforeRecovery.Id));
            Assert.That(restartDirectoryContent.Files.Select(file => file.Name), Is.EqualTo(new[] { "restarted-during-upload.txt" }));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploadedAfterRecovery.Id));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(uploadedAfterRecovery.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_RecoversAfterRemoteUploadBeforeBaselineUpdateThroughSdkAndServer()
    {
        CottonCloudClient client = CreateClient();
        await LoginAsync(client);
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync("sync-e2e-client-crash");
        string localRoot = Path.Combine(_tempDirectory, "client-crash");
        const string relativePath = "Crash/uploaded-before-baseline.txt";
        WriteLocalFile(localRoot, relativePath, "uploaded before baseline");
        SqliteSyncStateStore stateStore = CreateStateStore("client-crash-state.sqlite");
        var failingStateStore = new FailOnceUpsertStateStore(
            stateStore,
            new InvalidOperationException("simulated client crash before baseline update"));
        SyncEngine crashingEngine = CreateEngine(client, failingStateStore);
        var syncPair = new SyncPair
        {
            SyncPairId = "client-crash",
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        };

        InvalidOperationException? crashException = Assert.ThrowsAsync<InvalidOperationException>(() => crashingEngine.RunOnceAsync(syncPair));

        NodeFileManifestDto uploadedBeforeRecovery = await FindRemoteFileAsync(client, remoteRoot.Id, relativePath);
        string uploadedContent = await DownloadTextAsync(client, uploadedBeforeRecovery.Id);
        IReadOnlyList<SyncStateEntry> baselinesAfterCrash = await stateStore.LoadPairAsync(syncPair.SyncPairId);

        SyncEngine recoveredEngine = CreateEngine(client, stateStore);
        SyncRunResult recoveryResult = await recoveredEngine.RunOnceAsync(syncPair);

        NodeFileManifestDto uploadedAfterRecovery = await FindRemoteFileAsync(client, remoteRoot.Id, relativePath);
        NodeContentDto rootContent = await client.Nodes.GetChildrenAsync(remoteRoot.Id);
        NodeDto crashDirectory = rootContent.Nodes.Single(node => string.Equals(node.Name, "Crash", StringComparison.Ordinal));
        NodeContentDto crashDirectoryContent = await client.Nodes.GetChildrenAsync(crashDirectory.Id);
        SyncStateEntry? baseline = await stateStore.GetAsync(syncPair.SyncPairId, relativePath);

        Assert.Multiple(() =>
        {
            Assert.That(crashException, Is.Not.Null);
            Assert.That(failingStateStore.UpsertAttempts, Is.EqualTo(1));
            Assert.That(uploadedContent, Is.EqualTo("uploaded before baseline"));
            Assert.That(baselinesAfterCrash, Is.Empty);
            Assert.That(recoveryResult.Activities, Is.Empty);
            Assert.That(uploadedAfterRecovery.Id, Is.EqualTo(uploadedBeforeRecovery.Id));
            Assert.That(crashDirectoryContent.Files.Select(file => file.Name), Is.EqualTo(new[] { "uploaded-before-baseline.txt" }));
            Assert.That(baseline?.RemoteFileId, Is.EqualTo(uploadedAfterRecovery.Id));
            Assert.That(baseline?.RemoteContentHash, Is.EqualTo(uploadedAfterRecovery.ContentHash));
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

    private void RestartServerHost()
    {
        _httpClient?.Dispose();
        _factory?.Dispose();
        _factory = new TestAppFactory(CreateServerOverrides());
        _httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static Task<TokenPairDto> LoginAsync(CottonCloudClient client)
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

    private static SyncEngine CreateEngine(
        CottonCloudClient client,
        ISyncStateStore stateStore,
        IRemoteFileSynchronizer? remoteFiles = null)
    {
        return new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            remoteFiles ?? new SdkRemoteFileSynchronizer(client, new SdkRemoteFileSynchronizerOptions { ChunkSizeBytes = SyncTestChunkSizeBytes }),
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

    private static bool IsCaseSensitiveFileSystem(string directory)
    {
        string probeName = "case-probe-" + Guid.NewGuid().ToString("N");
        string probePath = Path.Combine(directory, probeName);
        File.WriteAllText(probePath, string.Empty);
        try
        {
            return !File.Exists(Path.Combine(directory, probeName.ToUpperInvariant()));
        }
        finally
        {
            File.Delete(probePath);
        }
    }

    private static IEnumerable<string> GetActivityPaths(SyncRunResult result, SyncActivityKind kind)
    {
        return result.Activities
            .Where(activity => activity.Kind == kind)
            .Select(activity => activity.RelativePath);
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value)
        {
            Values.Add(value);
        }
    }

    private static IEnumerable<string> GetDirectoryPrefixes(string relativeDirectoryPath)
    {
        string currentPath = string.Empty;
        foreach (string segment in relativeDirectoryPath.Split('/'))
        {
            currentPath = string.IsNullOrEmpty(currentPath)
                ? segment
                : $"{currentPath}/{segment}";
            yield return currentPath;
        }
    }

    private static SyncPairRunnerRetryOptions CreateNoDelayRetryOptions(int maxAttempts = 3)
    {
        return new SyncPairRunnerRetryOptions
        {
            MaxAttempts = maxAttempts,
            InitialDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero,
        };
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

    private sealed class ReleaseLockAfterFirstUnavailableSyncPairWork : ISyncPairWork
    {
        private readonly ISyncPairWork _inner;
        private readonly Action _releaseLock;

        public ReleaseLockAfterFirstUnavailableSyncPairWork(ISyncPairWork inner, Action releaseLock)
        {
            _inner = inner;
            _releaseLock = releaseLock;
        }

        public int RunCount { get; private set; }

        public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            RunCount++;
            try
            {
                await _inner.RunOnceAsync(syncPair, cancellationToken).ConfigureAwait(false);
            }
            catch (LocalFileUnavailableException) when (RunCount == 1)
            {
                _releaseLock();
                throw;
            }
        }
    }

    private sealed class FailOnceBeforeDelegatingSyncPairWork : ISyncPairWork
    {
        private readonly ISyncPairWork _inner;
        private readonly Exception _failure;

        public FailOnceBeforeDelegatingSyncPairWork(ISyncPairWork inner, Exception failure)
        {
            _inner = inner;
            _failure = failure;
        }

        public int RunCount { get; private set; }

        public async Task RunOnceAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            RunCount++;
            if (RunCount == 1)
            {
                throw _failure;
            }

            await _inner.RunOnceAsync(syncPair, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class FailOnceUpsertStateStore : ISyncStateStore
    {
        private readonly ISyncStateStore _inner;
        private readonly Exception _failure;
        private bool _hasFailed;

        public FailOnceUpsertStateStore(ISyncStateStore inner, Exception failure)
        {
            _inner = inner;
            _failure = failure;
        }

        public int UpsertAttempts { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return _inner.InitializeAsync(cancellationToken);
        }

        public Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(string syncPairId, CancellationToken cancellationToken = default)
        {
            return _inner.LoadPairAsync(syncPairId, cancellationToken);
        }

        public Task<SyncChangeCursor> GetChangeCursorAsync(string syncPairId, CancellationToken cancellationToken = default)
        {
            return _inner.GetChangeCursorAsync(syncPairId, cancellationToken);
        }

        public Task<SyncStateEntry?> GetAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
        {
            return _inner.GetAsync(syncPairId, relativePath, cancellationToken);
        }

        public Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default)
        {
            UpsertAttempts++;
            if (!_hasFailed)
            {
                _hasFailed = true;
                throw _failure;
            }

            return _inner.UpsertAsync(entry, cancellationToken);
        }

        public Task SaveChangeCursorAsync(SyncChangeCursor cursor, CancellationToken cancellationToken = default)
        {
            return _inner.SaveChangeCursorAsync(cursor, cancellationToken);
        }

        public Task DeleteAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(syncPairId, relativePath, cancellationToken);
        }

        public Task DeletePairAsync(string syncPairId, CancellationToken cancellationToken = default)
        {
            return _inner.DeletePairAsync(syncPairId, cancellationToken);
        }

        public Task ReplacePairAsync(
            string syncPairId,
            IReadOnlyCollection<SyncStateEntry> entries,
            CancellationToken cancellationToken = default)
        {
            return _inner.ReplacePairAsync(syncPairId, entries, cancellationToken);
        }
    }

    private sealed class RestartAfterUploadRemoteFileSynchronizer : IRemoteFileSynchronizer
    {
        private readonly IRemoteFileSynchronizer _inner;
        private readonly Action _restartServerHost;

        public RestartAfterUploadRemoteFileSynchronizer(IRemoteFileSynchronizer inner, Action restartServerHost)
        {
            _inner = inner;
            _restartServerHost = restartServerHost;
        }

        public int UploadAttempts { get; private set; }

        public async Task<NodeFileManifestDto> UploadFileAsync(
            Guid rootNodeId,
            string relativePath,
            LocalFileSnapshot localFile,
            NodeFileManifestDto? existingRemoteFile = null,
            CancellationToken cancellationToken = default)
        {
            UploadAttempts++;
            await _inner
                .UploadFileAsync(rootNodeId, relativePath, localFile, existingRemoteFile, cancellationToken)
                .ConfigureAwait(false);
            _restartServerHost();
            throw new HttpRequestException(
                "server restarted after upload acknowledgement was lost",
                null,
                System.Net.HttpStatusCode.ServiceUnavailable);
        }

        public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
        {
            return _inner.DownloadFileAsync(nodeFileId, destination, cancellationToken);
        }

        public Task DeleteFileAsync(
            Guid nodeFileId,
            bool skipTrash = false,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            return _inner.DeleteFileAsync(nodeFileId, skipTrash, expectedETag, cancellationToken);
        }
    }

    private sealed class DiskFullLocalFileSyncWriter : ILocalFileSyncWriter
    {
        public int WriteAttempts { get; private set; }

        public Task WriteFileAsync(
            string rootPath,
            string relativePath,
            Func<Stream, CancellationToken, Task> writeContentAsync,
            DateTime? lastWriteUtc = null,
            CancellationToken cancellationToken = default)
        {
            WriteAttempts++;
            throw new TestDiskFullIOException();
        }

        public Task DeleteFileAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Disk-full smoke does not delete local files.");
        }

        public Task CreateDirectoryAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return Task.CompletedTask;
        }

        public Task DeleteDirectoryAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Disk-full smoke does not delete local directories.");
        }

        public string CreateConflictRelativePath(string rootPath, string relativePath, DateTime timestampUtc)
        {
            throw new NotSupportedException("Disk-full smoke does not create conflicts.");
        }
    }

    private sealed class TestDiskFullIOException : IOException
    {
        public TestDiskFullIOException()
            : base("There is not enough space on the disk.")
        {
            HResult = unchecked((int)0x80070070);
        }
    }
}
