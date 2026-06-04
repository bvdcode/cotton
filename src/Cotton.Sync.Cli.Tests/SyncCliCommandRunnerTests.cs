// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;
using Cotton.Sync.Cli;
using Cotton.Sync.Cli.Tests.TestSupport;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli.Tests;

public sealed class SyncCliCommandRunnerTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-sync-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_PrintsHelpForEmptyArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync([], output, error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.ToString(), Does.Contain("state-summary"));
            Assert.That(output.ToString(), Does.Contain("sync-once"));
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task RunAsync_ReturnsErrorForMissingStateSummaryArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(["state-summary"], output, error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("--database"));
            Assert.That(error.ToString(), Does.Contain("--sync-pair"));
        });
    }

    [Test]
    public async Task RunAsync_ReturnsErrorForMissingSyncOnceArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(["sync-once"], output, error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("--server"));
            Assert.That(error.ToString(), Does.Contain("--remote-root"));
            Assert.That(error.ToString(), Does.Contain("--database"));
        });
    }

    [Test]
    public async Task RunAsync_ReturnsErrorForInvalidSyncOnceRemoteRoot()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            [
                "sync-once",
                "--server",
                "https://cloud.example.test/",
                "--username",
                "testuser",
                "--password",
                "testpassword",
                "--local-root",
                _tempDirectory,
                "--remote-root",
                "not-a-guid",
                "--sync-pair",
                "pair",
                "--database",
                Path.Combine(_tempDirectory, "sync-state.db"),
            ],
            output,
            error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("--remote-root"));
            Assert.That(error.ToString(), Does.Contain("GUID"));
        });
    }

    [Test]
    public async Task RunAsync_AcceptsBareSyncOnceServerHostBeforeRemoteRootValidation()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            [
                "sync-once",
                "--server",
                "app.cottoncloud.dev",
                "--username",
                "testuser",
                "--password",
                "testpassword",
                "--local-root",
                _tempDirectory,
                "--remote-root",
                "not-a-guid",
                "--sync-pair",
                "pair",
                "--database",
                Path.Combine(_tempDirectory, "sync-state.db"),
            ],
            output,
            error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("--remote-root"));
            Assert.That(error.ToString(), Does.Contain("GUID"));
            Assert.That(error.ToString(), Does.Not.Contain("--server"));
        });
    }

    [Test]
    public async Task RunAsync_ReturnsErrorForUnsupportedSyncOnceServerScheme()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            [
                "sync-once",
                "--server",
                "ftp://app.cottoncloud.dev",
                "--username",
                "testuser",
                "--password",
                "testpassword",
                "--local-root",
                _tempDirectory,
                "--remote-root",
                Guid.NewGuid().ToString("D"),
                "--sync-pair",
                "pair",
                "--database",
                Path.Combine(_tempDirectory, "sync-state.db"),
            ],
            output,
            error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("--server"));
            Assert.That(error.ToString(), Does.Contain("HTTP or HTTPS"));
        });
    }

    [Test]
    public async Task StateSummary_PrintsEntryCountAndCursor()
    {
        string databasePath = Path.Combine(_tempDirectory, "sync-state.db");
        string syncPairId = Guid.NewGuid().ToString("D");
        var store = new SqliteSyncStateStore(databasePath);
        await store.InitializeAsync();
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = syncPairId,
            RelativePath = "Documents/report.txt",
            Kind = SyncEntryKind.File,
            SyncedAtUtc = DateTime.UtcNow,
        });
        await store.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = syncPairId,
            LastCursor = 42,
            UpdatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
        });
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            ["state-summary", "--database", databasePath, "--sync-pair", syncPairId],
            output,
            error);

        string text = output.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(text, Does.Contain("Entries: 1"));
            Assert.That(text, Does.Contain("Remote cursor: 42"));
            Assert.That(text, Does.Contain(syncPairId));
        });
    }

    [Test]
    public async Task SyncOnce_UploadsLocalFileAndPersistsBaseline()
    {
        string localRoot = Path.Combine(_tempDirectory, "local");
        Directory.CreateDirectory(localRoot);
        const string relativePath = "hello.txt";
        byte[] content = Encoding.UTF8.GetBytes("hello from sync cli");
        string localFilePath = Path.Combine(localRoot, relativePath);
        File.WriteAllBytes(localFilePath, content);
        DateTime lastWriteUtc = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(localFilePath, lastWriteUtc);
        string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
        string databasePath = Path.Combine(_tempDirectory, "sync-state.db");
        string syncPairId = Guid.NewGuid().ToString("D");
        Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new SyncOnceUploadServerHandler(remoteRootId, relativePath, contentHash, content);
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            [
                "sync-once",
                "--server",
                "cotton.test",
                "--username",
                "testuser",
                "--password",
                "testpassword",
                "--local-root",
                localRoot,
                "--remote-root",
                remoteRootId.ToString("D"),
                "--sync-pair",
                syncPairId,
                "--database",
                databasePath,
            ],
            output,
            error,
            httpClient);

        var store = new SqliteSyncStateStore(databasePath);
        SyncStateEntry? entry = await store.GetAsync(syncPairId, relativePath);
        string text = output.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(text, Does.Contain("Cotton Sync one-shot run"));
            Assert.That(text, Does.Contain("Uploaded hello.txt"));
            Assert.That(text, Does.Contain("State entries: 1"));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Kind, Is.EqualTo(SyncEntryKind.File));
            Assert.That(entry.LocalContentHash, Is.EqualTo(contentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(contentHash));
            Assert.That(entry.RemoteFileId, Is.EqualTo(handler.CreatedFileId));
            Assert.That(handler.Requests.Select(static request => request.PathAndQuery), Is.EqualTo(new[]
            {
                "/api/v1/auth/login",
                "/api/v1/layouts/nodes/11111111-1111-1111-1111-111111111111",
                "/api/v1/layouts/nodes/11111111-1111-1111-1111-111111111111/children?page=1&pageSize=100&depth=0",
                "/api/v1/settings",
                "/api/v1/chunks/" + contentHash + "/exists",
                "/api/v1/chunks/raw?hash=" + contentHash,
                "/api/v1/files/from-chunks",
            }));
        });
    }

    [Test]
    public async Task SyncOnce_UploadsEmptyLocalDirectoryAndPersistsDirectoryBaseline()
    {
        string localRoot = Path.Combine(_tempDirectory, "local-empty-directory");
        const string relativePath = "Projects";
        Directory.CreateDirectory(Path.Combine(localRoot, relativePath));
        string databasePath = Path.Combine(_tempDirectory, "sync-state.db");
        string syncPairId = Guid.NewGuid().ToString("D");
        Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new SyncOnceDirectoryServerHandler(remoteRootId, relativePath);
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            [
                "sync-once",
                "--server",
                "cotton.test",
                "--username",
                "testuser",
                "--password",
                "testpassword",
                "--local-root",
                localRoot,
                "--remote-root",
                remoteRootId.ToString("D"),
                "--sync-pair",
                syncPairId,
                "--database",
                databasePath,
            ],
            output,
            error,
            httpClient);

        var store = new SqliteSyncStateStore(databasePath);
        SyncStateEntry? entry = await store.GetAsync(syncPairId, relativePath);
        string text = output.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(text, Does.Contain("Uploaded Projects - Created remote folder."));
            Assert.That(text, Does.Contain("State entries: 1"));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Kind, Is.EqualTo(SyncEntryKind.Directory));
            Assert.That(entry.RemoteNodeId, Is.EqualTo(handler.CreatedDirectoryId));
            Assert.That(entry.LocalContentHash, Is.Null);
            Assert.That(entry.RemoteContentHash, Is.Null);
            Assert.That(handler.Requests.Select(static request => request.PathAndQuery), Is.EqualTo(new[]
            {
                "/api/v1/auth/login",
                "/api/v1/layouts/nodes/11111111-1111-1111-1111-111111111111",
                "/api/v1/layouts/nodes/11111111-1111-1111-1111-111111111111/children?page=1&pageSize=100&depth=0",
                "/api/v1/layouts/nodes",
            }));
        });
    }

}
