// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cotton.Auth;
using Cotton.Sync.Cli;
using Cotton.Sync.Cli.Tests.TestSupport;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli.Tests
{

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
                Assert.That(output.ToString(), Does.Contain("auth-browser"));
                Assert.That(output.ToString(), Does.Contain("sync-once"));
                Assert.That(output.ToString(), Does.Contain("sync-soak"));
                Assert.That(error.ToString(), Is.Empty);
            });
        }

        [Test]
        public async Task AuthBrowser_PrintsApprovalUrlAndSignedInAccount()
        {
            var handler = new AppCodeAuthServerHandler();
            using var httpClient = new HttpClient(handler);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "auth-browser",
                    "--server",
                    "cotton.test",
                    "--application-version",
                    "1.2.3",
                    "--device-name",
                    "workstation",
                ],
                output,
                error,
                httpClient);

            string text = output.ToString();
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error.ToString(), Is.Empty);
                Assert.That(text, Does.Contain("Cotton Sync browser sign-in"));
                Assert.That(text, Does.Contain("Approval URL: https://cotton.test/oauth/app-code/0190a000-0000-7000-8000-000000000022"));
                Assert.That(text, Does.Contain("Signed in: browser@example.test"));
                Assert.That(text, Does.Contain("Signed out."));
                Assert.That(handler.Requests.Select(static request => request.PathAndQuery), Is.EqualTo(new[]
                {
                    "/api/v1/oauth/app-code/start",
                    "/api/v1/oauth/app-code/poll",
                    "/api/v1/auth/me",
                    "/api/v1/auth/logout?refreshToken=refresh-token",
                }));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"applicationName\":\"Cotton Sync CLI\""));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"applicationVersion\":\"1.2.3\""));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"deviceName\":\"workstation\""));
            });
        }

        [Test]
        public async Task AuthBrowser_ReturnsFailureForDeniedApproval()
        {
            var handler = new AppCodeAuthServerHandler(deny: true);
            using var httpClient = new HttpClient(handler);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "auth-browser",
                    "--server",
                    "https://cotton.test/",
                ],
                output,
                error,
                httpClient);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(output.ToString(), Does.Contain("Approval URL: https://cotton.test/oauth/app-code/0190a000-0000-7000-8000-000000000022"));
                Assert.That(error.ToString(), Does.Contain("Browser sign-in was denied."));
                Assert.That(error.ToString(), Does.Contain("denied"));
                Assert.That(handler.Requests.Select(static request => request.PathAndQuery), Is.EqualTo(new[]
                {
                    "/api/v1/oauth/app-code/start",
                    "/api/v1/oauth/app-code/poll",
                }));
            });
        }

        [Test]
        public async Task AuthBrowser_ReturnsErrorForMissingServer()
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(["auth-browser"], output, error);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(output.ToString(), Is.Empty);
                Assert.That(error.ToString(), Does.Contain("--server"));
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
        public async Task RunAsync_ReturnsErrorForMissingSyncSoakLimiter()
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-soak",
                    "--server",
                    "https://cloud.example.test/",
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
                Assert.That(error.ToString(), Does.Contain("--iterations"));
                Assert.That(error.ToString(), Does.Contain("--duration-seconds"));
            });
        }

        [Test]
        public async Task RunAsync_ReturnsErrorForInvalidSyncSoakProbeFile()
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-soak",
                    "--server",
                    "https://cloud.example.test/",
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
                    "--iterations",
                    "1",
                    "--probe-file",
                    "../outside.txt",
                ],
                output,
                error);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(output.ToString(), Is.Empty);
                Assert.That(error.ToString(), Does.Contain("--probe-file"));
            });
        }

        [Test]
        public async Task RunAsync_ReturnsErrorForIncompleteSyncSoakSecondClient()
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-soak",
                    "--server",
                    "https://cloud.example.test/",
                    "--username",
                    "testuser",
                    "--password",
                    "testpassword",
                    "--local-root",
                    _tempDirectory,
                    "--remote-root",
                    Guid.NewGuid().ToString("D"),
                    "--sync-pair",
                    "pair-a",
                    "--database",
                    Path.Combine(_tempDirectory, "sync-state-a.db"),
                    "--iterations",
                    "1",
                    "--second-local-root",
                    Path.Combine(_tempDirectory, "second"),
                ],
                output,
                error);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(output.ToString(), Is.Empty);
                Assert.That(error.ToString(), Does.Contain("--second-local-root"));
                Assert.That(error.ToString(), Does.Contain("--second-sync-pair"));
                Assert.That(error.ToString(), Does.Contain("--second-database"));
            });
        }

        [Test]
        public async Task RunAsync_ReturnsErrorForUnsafeSyncSoakSecondClientInputs()
        {
            string firstLocalRoot = Path.Combine(_tempDirectory, "client-a");
            string secondLocalRoot = Path.Combine(_tempDirectory, "client-b");
            string nestedSecondLocalRoot = Path.Combine(firstLocalRoot, "nested");
            string firstDatabasePath = Path.Combine(_tempDirectory, "client-a.db");
            string secondDatabasePath = Path.Combine(_tempDirectory, "client-b.db");
            var cases = new[]
            {
                new
                {
                    SecondLocalRoot = firstLocalRoot,
                    SecondSyncPairId = "pair-b",
                    SecondDatabasePath = secondDatabasePath,
                    ExpectedMessage = "local roots",
                },
                new
                {
                    SecondLocalRoot = nestedSecondLocalRoot,
                    SecondSyncPairId = "pair-b",
                    SecondDatabasePath = secondDatabasePath,
                    ExpectedMessage = "local roots",
                },
                new
                {
                    SecondLocalRoot = secondLocalRoot,
                    SecondSyncPairId = "pair-a",
                    SecondDatabasePath = secondDatabasePath,
                    ExpectedMessage = "sync pair ids",
                },
                new
                {
                    SecondLocalRoot = secondLocalRoot,
                    SecondSyncPairId = "pair-b",
                    SecondDatabasePath = firstDatabasePath,
                    ExpectedMessage = "databases",
                },
            };

            foreach (var testCase in cases)
            {
                using var output = new StringWriter();
                using var error = new StringWriter();

                int exitCode = await SyncCliCommandRunner.RunAsync(
                    [
                        "sync-soak",
                        "--server",
                        "https://cloud.example.test/",
                        "--username",
                        "testuser",
                        "--password",
                        "testpassword",
                        "--local-root",
                        firstLocalRoot,
                        "--remote-root",
                        Guid.NewGuid().ToString("D"),
                        "--sync-pair",
                        "pair-a",
                        "--database",
                        firstDatabasePath,
                        "--iterations",
                        "1",
                        "--second-local-root",
                        testCase.SecondLocalRoot,
                        "--second-sync-pair",
                        testCase.SecondSyncPairId,
                        "--second-database",
                        testCase.SecondDatabasePath,
                    ],
                    output,
                    error);

                Assert.Multiple(() =>
                {
                    Assert.That(exitCode, Is.EqualTo(2));
                    Assert.That(output.ToString(), Is.Empty);
                    Assert.That(error.ToString(), Does.Contain(testCase.ExpectedMessage));
                });
            }
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
                    "/api/v1/auth/logout?refreshToken=refresh-token",
                }));
            });
        }

        [Test]
        public async Task SyncOnce_WithBrowserLoginUploadsLocalFileAndPersistsBaseline()
        {
            string localRoot = Path.Combine(_tempDirectory, "local-browser");
            Directory.CreateDirectory(localRoot);
            const string relativePath = "hello-browser.txt";
            byte[] content = Encoding.UTF8.GetBytes("hello from browser sync cli");
            string localFilePath = Path.Combine(localRoot, relativePath);
            File.WriteAllBytes(localFilePath, content);
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            string databasePath = Path.Combine(_tempDirectory, "sync-state-browser.db");
            string syncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var handler = new SyncOnceUploadServerHandler(
                remoteRootId,
                relativePath,
                contentHash,
                content,
                allowAppCodeAuth: true);
            using var httpClient = new HttpClient(handler);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-once",
                    "--server",
                    "cotton.test",
                    "--browser-login",
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
                Assert.That(text, Does.Contain("Approval URL: https://cotton.test/oauth/app-code/0190a000-0000-7000-8000-000000000022"));
                Assert.That(text, Does.Contain("Cotton Sync one-shot run"));
                Assert.That(text, Does.Contain("Uploaded hello-browser.txt"));
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry!.LocalContentHash, Is.EqualTo(contentHash));
                Assert.That(handler.Requests.Select(static request => request.PathAndQuery), Is.EqualTo(new[]
                {
                    "/api/v1/oauth/app-code/start",
                    "/api/v1/oauth/app-code/poll",
                    "/api/v1/auth/me",
                    "/api/v1/layouts/nodes/11111111-1111-1111-1111-111111111111",
                    "/api/v1/layouts/nodes/11111111-1111-1111-1111-111111111111/children?page=1&pageSize=100&depth=0",
                    "/api/v1/settings",
                    "/api/v1/chunks/" + contentHash + "/exists",
                    "/api/v1/chunks/raw?hash=" + contentHash,
                    "/api/v1/files/from-chunks",
                    "/api/v1/auth/logout?refreshToken=refresh-token",
                }));
            });
        }

        [Test]
        public async Task SyncOnce_ExternalProcessRecoversAfterRemoteUploadBeforeBaselineUpdate()
        {
            string localRoot = Path.Combine(_tempDirectory, "process-crash-local");
            Directory.CreateDirectory(localRoot);
            const string relativePath = "crash-recovery.txt";
            byte[] content = Encoding.UTF8.GetBytes("uploaded before process crash");
            string localFilePath = Path.Combine(localRoot, relativePath);
            File.WriteAllBytes(localFilePath, content);
            File.SetLastWriteTimeUtc(localFilePath, new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc));
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            string databasePath = Path.Combine(_tempDirectory, "process-crash-state.db");
            string syncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            await using var server = new SyncProcessCrashHttpServer(remoteRootId, relativePath, contentHash, content);
            string[] args = CreateSyncOnceProcessArgs(server.BaseUri, localRoot, remoteRootId, syncPairId, databasePath);

            using Process crashingProcess = StartCliProcess(args);
            Task<string> firstOutputTask = crashingProcess.StandardOutput.ReadToEndAsync();
            Task<string> firstErrorTask = crashingProcess.StandardError.ReadToEndAsync();
            try
            {
                await server.WaitForFileCommittedAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                KillProcessTree(crashingProcess);
                await WaitForProcessExitAsync(crashingProcess, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            finally
            {
                server.ReleaseBlockedCreateResponse();
                KillProcessTree(crashingProcess);
            }

            _ = await firstOutputTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            _ = await firstErrorTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            var storeAfterCrash = new SqliteSyncStateStore(databasePath);
            IReadOnlyList<SyncStateEntry> entriesAfterCrash = await storeAfterCrash.LoadPairAsync(syncPairId);

            using Process recoveryProcess = StartCliProcess(args);
            Task<string> recoveryOutputTask = recoveryProcess.StandardOutput.ReadToEndAsync();
            Task<string> recoveryErrorTask = recoveryProcess.StandardError.ReadToEndAsync();
            await WaitForProcessExitAsync(recoveryProcess, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            string recoveryOutput = await recoveryOutputTask.ConfigureAwait(false);
            string recoveryError = await recoveryErrorTask.ConfigureAwait(false);

            var storeAfterRecovery = new SqliteSyncStateStore(databasePath);
            SyncStateEntry? entry = await storeAfterRecovery.GetAsync(syncPairId, relativePath);
            IReadOnlyList<HttpRequestSnapshot> requests = server.Requests;
            server.AssertNoFaults();

            Assert.Multiple(() =>
            {
                Assert.That(crashingProcess.ExitCode, Is.Not.EqualTo(0));
                Assert.That(entriesAfterCrash, Is.Empty);
                Assert.That(recoveryProcess.ExitCode, Is.EqualTo(0), recoveryError);
                Assert.That(recoveryError, Is.Empty);
                Assert.That(recoveryOutput, Does.Contain("Activities: 0"));
                Assert.That(recoveryOutput, Does.Contain("State entries: 1"));
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry!.Kind, Is.EqualTo(SyncEntryKind.File));
                Assert.That(entry.LocalContentHash, Is.EqualTo(contentHash));
                Assert.That(entry.RemoteContentHash, Is.EqualTo(contentHash));
                Assert.That(entry.RemoteFileId, Is.EqualTo(server.CreatedFileId));
                Assert.That(
                    requests.Count(static request => request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/files/from-chunks"),
                    Is.EqualTo(1));
            });
        }

        [Test]
        public async Task SyncOnce_ExternalProcessRecoversAfterCrashDuringRemoteDownload()
        {
            string localRoot = Path.Combine(_tempDirectory, "process-download-crash-local");
            Directory.CreateDirectory(localRoot);
            const string relativePath = "remote-download-crash.txt";
            byte[] content = Encoding.UTF8.GetBytes("download interrupted before the first process can finish");
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            string databasePath = Path.Combine(_tempDirectory, "process-download-crash-state.db");
            string syncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            string targetPath = Path.Combine(localRoot, relativePath);
            string temporaryDirectory = Path.Combine(localRoot, ".cotton-sync", "tmp");
            await using var server = new SyncProcessDownloadCrashHttpServer(remoteRootId, relativePath, contentHash, content);
            string[] args = CreateSyncOnceProcessArgs(server.BaseUri, localRoot, remoteRootId, syncPairId, databasePath);

            using Process crashingProcess = StartCliProcess(args);
            Task<string> firstOutputTask = crashingProcess.StandardOutput.ReadToEndAsync();
            Task<string> firstErrorTask = crashingProcess.StandardError.ReadToEndAsync();
            try
            {
                await server.WaitForFirstDownloadStartedAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                await WaitForTemporaryDownloadAsync(temporaryDirectory, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                KillProcessTree(crashingProcess);
                await WaitForProcessExitAsync(crashingProcess, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            finally
            {
                server.ReleaseFirstDownload();
                KillProcessTree(crashingProcess);
            }

            _ = await firstOutputTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            _ = await firstErrorTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            var store = new SqliteSyncStateStore(databasePath);
            SyncStateEntry? entryAfterCrash = await store.GetAsync(syncPairId, relativePath);
            string[] staleTemporaryFiles = ListTemporaryDownloads(temporaryDirectory);
            bool targetExistsAfterCrash = File.Exists(targetPath);

            using Process recoveryProcess = StartCliProcess(args);
            Task<string> recoveryOutputTask = recoveryProcess.StandardOutput.ReadToEndAsync();
            Task<string> recoveryErrorTask = recoveryProcess.StandardError.ReadToEndAsync();
            await WaitForProcessExitAsync(recoveryProcess, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            string recoveryOutput = await recoveryOutputTask.ConfigureAwait(false);
            string recoveryError = await recoveryErrorTask.ConfigureAwait(false);

            SyncStateEntry? entryAfterRecovery = await store.GetAsync(syncPairId, relativePath);
            string[] remainingTemporaryFiles = ListTemporaryDownloads(temporaryDirectory);
            IReadOnlyList<HttpRequestSnapshot> requests = server.Requests;
            string downloadPath = "/api/v1/files/" + server.RemoteFileId.ToString("D") + "/content?download=false";
            server.AssertNoFaults();

            Assert.Multiple(() =>
            {
                Assert.That(crashingProcess.ExitCode, Is.Not.EqualTo(0));
                Assert.That(targetExistsAfterCrash, Is.False);
                Assert.That(entryAfterCrash, Is.Null);
                Assert.That(staleTemporaryFiles, Is.Not.Empty);
                Assert.That(recoveryProcess.ExitCode, Is.EqualTo(0), recoveryError);
                Assert.That(recoveryError, Is.Empty);
                Assert.That(recoveryOutput, Does.Contain("Downloaded remote-download-crash.txt"));
                Assert.That(recoveryOutput, Does.Contain("State entries: 1"));
                Assert.That(File.ReadAllBytes(targetPath), Is.EqualTo(content));
                Assert.That(entryAfterRecovery, Is.Not.Null);
                Assert.That(entryAfterRecovery!.RemoteFileId, Is.EqualTo(server.RemoteFileId));
                Assert.That(entryAfterRecovery.RemoteContentHash, Is.EqualTo(contentHash));
                Assert.That(remainingTemporaryFiles, Is.Empty);
                Assert.That(
                    requests.Count(request => request.Method == HttpMethod.Get && request.PathAndQuery == downloadPath),
                    Is.EqualTo(2));
            });
        }

        [Test]
        public async Task SyncOnce_ExternalProcessRecoversAfterRemoteDeleteBeforeBaselineDelete()
        {
            string localRoot = Path.Combine(_tempDirectory, "process-delete-crash-local");
            Directory.CreateDirectory(localRoot);
            const string relativePath = "remote-delete-crash.txt";
            byte[] content = Encoding.UTF8.GetBytes("remote delete before baseline");
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            string databasePath = Path.Combine(_tempDirectory, "process-delete-crash-state.db");
            string syncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            await using var server = new SyncProcessRemoteDeleteCrashHttpServer(remoteRootId, relativePath, contentHash);
            var store = new SqliteSyncStateStore(databasePath);
            await store.UpsertAsync(new SyncStateEntry
            {
                SyncPairId = syncPairId,
                RelativePath = relativePath,
                Kind = SyncEntryKind.File,
                LocalContentHash = contentHash,
                LocalLastWriteUtc = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc),
                RemoteFileId = server.RemoteFileId,
                RemoteContentHash = contentHash,
                RemoteETag = "sha256-" + contentHash,
                SyncedAtUtc = new DateTime(2026, 6, 4, 12, 1, 0, DateTimeKind.Utc),
            });
            string[] args = CreateSyncOnceProcessArgs(server.BaseUri, localRoot, remoteRootId, syncPairId, databasePath);

            using Process crashingProcess = StartCliProcess(args);
            Task<string> firstOutputTask = crashingProcess.StandardOutput.ReadToEndAsync();
            Task<string> firstErrorTask = crashingProcess.StandardError.ReadToEndAsync();
            try
            {
                await server.WaitForFileDeletedAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                KillProcessTree(crashingProcess);
                await WaitForProcessExitAsync(crashingProcess, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            finally
            {
                server.ReleaseBlockedDeleteResponse();
                KillProcessTree(crashingProcess);
            }

            _ = await firstOutputTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            _ = await firstErrorTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            SyncStateEntry? entryAfterCrash = await store.GetAsync(syncPairId, relativePath);

            using Process recoveryProcess = StartCliProcess(args);
            Task<string> recoveryOutputTask = recoveryProcess.StandardOutput.ReadToEndAsync();
            Task<string> recoveryErrorTask = recoveryProcess.StandardError.ReadToEndAsync();
            await WaitForProcessExitAsync(recoveryProcess, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            string recoveryOutput = await recoveryOutputTask.ConfigureAwait(false);
            string recoveryError = await recoveryErrorTask.ConfigureAwait(false);

            IReadOnlyList<SyncStateEntry> entriesAfterRecovery = await store.LoadPairAsync(syncPairId);
            IReadOnlyList<HttpRequestSnapshot> requests = server.Requests;
            string deletePath = "/api/v1/files/" + server.RemoteFileId.ToString("D") + "?skipTrash=false";
            server.AssertNoFaults();

            Assert.Multiple(() =>
            {
                Assert.That(crashingProcess.ExitCode, Is.Not.EqualTo(0));
                Assert.That(entryAfterCrash, Is.Not.Null);
                Assert.That(entryAfterCrash!.RemoteFileId, Is.EqualTo(server.RemoteFileId));
                Assert.That(recoveryProcess.ExitCode, Is.EqualTo(0), recoveryError);
                Assert.That(recoveryError, Is.Empty);
                Assert.That(recoveryOutput, Does.Contain("Activities: 0"));
                Assert.That(recoveryOutput, Does.Contain("State entries: 0"));
                Assert.That(entriesAfterRecovery, Is.Empty);
                Assert.That(
                    requests.Count(request => request.Method == HttpMethod.Delete && request.PathAndQuery == deletePath),
                    Is.EqualTo(1));
            });
        }

        [Test]
        public async Task SyncSoak_RunsOneIterationAndPrintsSummary()
        {
            string localRoot = Path.Combine(_tempDirectory, "soak-local");
            Directory.CreateDirectory(localRoot);
            const string relativePath = "soak.txt";
            byte[] content = Encoding.UTF8.GetBytes("hello from sync soak");
            string localFilePath = Path.Combine(localRoot, relativePath);
            File.WriteAllBytes(localFilePath, content);
            File.SetLastWriteTimeUtc(localFilePath, new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc));
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            string databasePath = Path.Combine(_tempDirectory, "sync-soak-state.db");
            string syncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var handler = new SyncOnceUploadServerHandler(remoteRootId, relativePath, contentHash, content);
            using var httpClient = new HttpClient(handler);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-soak",
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
                    "--iterations",
                    "1",
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
                Assert.That(text, Does.Contain("Cotton Sync soak run"));
                Assert.That(text, Does.Contain("Iteration 1: activities=1, stateEntries=1"));
                Assert.That(text, Does.Contain("elapsedSeconds="));
                Assert.That(text, Does.Contain("Elapsed seconds:"));
                Assert.That(text, Does.Contain("CPU seconds:"));
                Assert.That(text, Does.Contain("CPU utilization percent:"));
                Assert.That(text, Does.Contain("Start working set bytes:"));
                Assert.That(text, Does.Contain("End working set bytes:"));
                Assert.That(text, Does.Contain("Working set growth bytes:"));
                Assert.That(text, Does.Contain("Peak working set bytes:"));
                Assert.That(text, Does.Contain("Peak working set growth bytes:"));
                Assert.That(text, Does.Contain("Start managed memory bytes:"));
                Assert.That(text, Does.Contain("End managed memory bytes:"));
                Assert.That(text, Does.Contain("Managed memory growth bytes:"));
                Assert.That(text, Does.Contain("Peak managed memory bytes:"));
                Assert.That(text, Does.Contain("Peak managed memory growth bytes:"));
                Assert.That(text, Does.Contain("Iterations completed: 1"));
                Assert.That(text, Does.Contain("Iteration seconds total:"));
                Assert.That(text, Does.Contain("Iteration seconds average:"));
                Assert.That(text, Does.Contain("Iteration seconds max:"));
                Assert.That(text, Does.Contain("Final convergence activities: 0"));
                Assert.That(text, Does.Contain("Final state entries: 1"));
                Assert.That(text, Does.Contain("Sync errors: 0"));
                Assert.That(text, Does.Contain("Converged: yes"));
                Assert.That(text, Does.Contain("Failures: 0"));
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry!.RemoteFileId, Is.EqualTo(handler.CreatedFileId));
                Assert.That(entry.RemoteContentHash, Is.EqualTo(contentHash));
            });
        }

        [Test]
        public async Task SyncSoak_ReturnsFailureAndSummaryWhenSyncPassThrows()
        {
            string localRoot = Path.Combine(_tempDirectory, "soak-failing-local");
            Directory.CreateDirectory(localRoot);
            const string relativePath = "soak-failing.txt";
            byte[] content = Encoding.UTF8.GetBytes("unexpected content hash");
            string localFilePath = Path.Combine(localRoot, relativePath);
            File.WriteAllBytes(localFilePath, content);
            File.SetLastWriteTimeUtc(localFilePath, new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc));
            string databasePath = Path.Combine(_tempDirectory, "sync-soak-failing-state.db");
            string syncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var handler = new SyncOnceUploadServerHandler(
                remoteRootId,
                relativePath,
                expectedContentHash: "unexpected-hash",
                expectedContent: content);
            using var httpClient = new HttpClient(handler);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-soak",
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
                    "--iterations",
                    "1",
                ],
                output,
                error,
                httpClient);

            string text = output.ToString();
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(error.ToString(), Is.Empty);
                Assert.That(text, Does.Contain("Sync error: InvalidOperationException: Unexpected request:"));
                Assert.That(text, Does.Contain("Elapsed seconds:"));
                Assert.That(text, Does.Contain("CPU seconds:"));
                Assert.That(text, Does.Contain("CPU utilization percent:"));
                Assert.That(text, Does.Contain("Start working set bytes:"));
                Assert.That(text, Does.Contain("End working set bytes:"));
                Assert.That(text, Does.Contain("Working set growth bytes:"));
                Assert.That(text, Does.Contain("Peak working set bytes:"));
                Assert.That(text, Does.Contain("Peak working set growth bytes:"));
                Assert.That(text, Does.Contain("Start managed memory bytes:"));
                Assert.That(text, Does.Contain("End managed memory bytes:"));
                Assert.That(text, Does.Contain("Managed memory growth bytes:"));
                Assert.That(text, Does.Contain("Peak managed memory bytes:"));
                Assert.That(text, Does.Contain("Peak managed memory growth bytes:"));
                Assert.That(text, Does.Contain("Iterations completed: 0"));
                Assert.That(text, Does.Contain("Iteration seconds total: 0"));
                Assert.That(text, Does.Contain("Iteration seconds average: 0"));
                Assert.That(text, Does.Contain("Iteration seconds max: 0"));
                Assert.That(text, Does.Contain("Total activities: 0"));
                Assert.That(text, Does.Contain("Sync errors: 1"));
                Assert.That(text, Does.Contain("Final convergence activities: not run"));
                Assert.That(text, Does.Contain("Final state entries: not run"));
                Assert.That(text, Does.Contain("Converged: no"));
                Assert.That(text, Does.Contain("Failures: 1"));
            });
        }

        [Test]
        public async Task SyncSoak_ReturnsFailureWhenFinalConvergenceStillHasActivities()
        {
            string localRoot = Path.Combine(_tempDirectory, "soak-non-converged-local");
            Directory.CreateDirectory(localRoot);
            const string relativePath = "soak-non-converged.txt";
            byte[] content = Encoding.UTF8.GetBytes("remote never reports this file");
            string localFilePath = Path.Combine(localRoot, relativePath);
            File.WriteAllBytes(localFilePath, content);
            File.SetLastWriteTimeUtc(localFilePath, new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc));
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            string databasePath = Path.Combine(_tempDirectory, "sync-soak-non-converged-state.db");
            string syncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var handler = new SyncOnceUploadServerHandler(
                remoteRootId,
                relativePath,
                contentHash,
                content,
                exposeCreatedFileInChildren: false);
            using var httpClient = new HttpClient(handler);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-soak",
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
                    "--iterations",
                    "1",
                ],
                output,
                error,
                httpClient);

            string text = output.ToString();
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(error.ToString(), Is.Empty);
                Assert.That(text, Does.Contain("Iteration 1: activities=1, stateEntries=1"));
                Assert.That(text, Does.Contain("Final convergence activities: 1"));
                Assert.That(text, Does.Contain("Sync errors: 0"));
                Assert.That(text, Does.Contain("Converged: no"));
                Assert.That(text, Does.Contain("Failures: 1"));
            });
        }

        [Test]
        public async Task SyncSoak_TwoClientModePropagatesClientAChangeToClientBAndConverges()
        {
            string firstLocalRoot = Path.Combine(_tempDirectory, "soak-two-client-a");
            string secondLocalRoot = Path.Combine(_tempDirectory, "soak-two-client-b");
            Directory.CreateDirectory(firstLocalRoot);
            Directory.CreateDirectory(secondLocalRoot);
            const string relativePath = "soak-two-client.txt";
            byte[] content = Encoding.UTF8.GetBytes("hello from client A");
            string firstLocalFilePath = Path.Combine(firstLocalRoot, relativePath);
            File.WriteAllBytes(firstLocalFilePath, content);
            File.SetLastWriteTimeUtc(firstLocalFilePath, new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc));
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            string firstDatabasePath = Path.Combine(_tempDirectory, "sync-soak-two-client-a.db");
            string secondDatabasePath = Path.Combine(_tempDirectory, "sync-soak-two-client-b.db");
            string firstSyncPairId = Guid.NewGuid().ToString("D");
            string secondSyncPairId = Guid.NewGuid().ToString("D");
            Guid remoteRootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var handler = new SyncOnceUploadServerHandler(remoteRootId, relativePath, contentHash, content);
            using var httpClient = new HttpClient(handler);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await SyncCliCommandRunner.RunAsync(
                [
                    "sync-soak",
                    "--server",
                    "cotton.test",
                    "--username",
                    "testuser",
                    "--password",
                    "testpassword",
                    "--local-root",
                    firstLocalRoot,
                    "--remote-root",
                    remoteRootId.ToString("D"),
                    "--sync-pair",
                    firstSyncPairId,
                    "--database",
                    firstDatabasePath,
                    "--iterations",
                    "1",
                    "--second-local-root",
                    secondLocalRoot,
                    "--second-sync-pair",
                    secondSyncPairId,
                    "--second-database",
                    secondDatabasePath,
                ],
                output,
                error,
                httpClient);

            var firstStore = new SqliteSyncStateStore(firstDatabasePath);
            var secondStore = new SqliteSyncStateStore(secondDatabasePath);
            SyncStateEntry? firstEntry = await firstStore.GetAsync(firstSyncPairId, relativePath);
            SyncStateEntry? secondEntry = await secondStore.GetAsync(secondSyncPairId, relativePath);
            string secondLocalFilePath = Path.Combine(secondLocalRoot, relativePath);
            string text = output.ToString();
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error.ToString(), Is.Empty);
                Assert.That(text, Does.Contain("Cotton Sync soak run"));
                Assert.That(text, Does.Contain("Sync pair: " + firstSyncPairId));
                Assert.That(text, Does.Contain("Second sync pair: " + secondSyncPairId));
                Assert.That(
                    text,
                    Does.Contain("Iteration 1: clientAActivities=1, clientBActivities=1, clientAStateEntries=1, clientBStateEntries=1"));
                Assert.That(text, Does.Contain("Total activities: 2"));
                Assert.That(text, Does.Contain("Final convergence activities: 0"));
                Assert.That(text, Does.Contain("Final state entries: 2"));
                Assert.That(text, Does.Contain("Converged: yes"));
                Assert.That(File.Exists(secondLocalFilePath), Is.True);
                Assert.That(File.ReadAllBytes(secondLocalFilePath), Is.EqualTo(content));
                Assert.That(firstEntry, Is.Not.Null);
                Assert.That(secondEntry, Is.Not.Null);
                Assert.That(firstEntry!.RemoteFileId, Is.EqualTo(handler.CreatedFileId));
                Assert.That(secondEntry!.RemoteFileId, Is.EqualTo(handler.CreatedFileId));
                Assert.That(firstEntry.RemoteContentHash, Is.EqualTo(contentHash));
                Assert.That(secondEntry.RemoteContentHash, Is.EqualTo(contentHash));
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
                    "/api/v1/auth/logout?refreshToken=refresh-token",
                }));
            });
        }

        private static string[] CreateSyncOnceProcessArgs(
            Uri serverUri,
            string localRoot,
            Guid remoteRootId,
            string syncPairId,
            string databasePath)
        {
            return
            [
                "sync-once",
                "--server",
                serverUri.AbsoluteUri,
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
            ];
        }

        private static Process StartCliProcess(IEnumerable<string> args)
        {
            string cliPath = typeof(SyncCliCommandRunner).Assembly.Location;
            var startInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(cliPath);
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Cotton Sync CLI process.");
        }

        private static async Task WaitForProcessExitAsync(Process process, TimeSpan timeout)
        {
            try
            {
                await process.WaitForExitAsync().WaitAsync(timeout).ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                KillProcessTree(process);
                throw new AssertionException("Cotton Sync CLI process did not exit within " + timeout.TotalSeconds.ToStringInvariant() + " seconds.", exception);
            }
        }

        private static async Task WaitForTemporaryDownloadAsync(string temporaryDirectory, TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    if (ListTemporaryDownloads(temporaryDirectory).Length > 0)
                    {
                        return;
                    }

                    await Task.Delay(25, cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }

            throw new AssertionException("Cotton Sync CLI process did not create a temporary download file within "
                + timeout.TotalSeconds.ToStringInvariant()
                + " seconds.");
        }

        private static string[] ListTemporaryDownloads(string temporaryDirectory)
        {
            return Directory.Exists(temporaryDirectory)
                ? Directory.GetFiles(temporaryDirectory, "*.download", SearchOption.AllDirectories)
                : [];
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private sealed class AppCodeAuthServerHandler : HttpMessageHandler
        {
            private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
            private readonly bool _deny;
            private int _pollCount;

            public AppCodeAuthServerHandler(bool deny = false)
            {
                _deny = deny;
            }

            public List<HttpRequestSnapshot> Requests { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                byte[] rawBody = request.Content is null
                    ? []
                    : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                string body = Encoding.UTF8.GetString(rawBody);
                var snapshot = new HttpRequestSnapshot(
                    request.Method,
                    request.RequestUri?.PathAndQuery ?? string.Empty,
                    request.Headers.Authorization?.Parameter,
                    body,
                    rawBody);
                Requests.Add(snapshot);

                if (snapshot.Method == HttpMethod.Post && snapshot.PathAndQuery == "/api/v1/oauth/app-code/start")
                {
                    return Json(HttpStatusCode.OK, new
                    {
                        approvalId = Guid.Parse("0190a000-0000-7000-8000-000000000022"),
                        approvalUrl = "/oauth/app-code/0190a000-0000-7000-8000-000000000022",
                        pollToken = "poll-token",
                        expiresAt = DateTime.UtcNow.AddMinutes(10),
                        pollIntervalSeconds = 1,
                    });
                }

                if (snapshot.Method == HttpMethod.Post && snapshot.PathAndQuery == "/api/v1/oauth/app-code/poll")
                {
                    Assert.That(snapshot.Body, Does.Contain("\"pollToken\":\"poll-token\""));
                    _pollCount++;
                    return _deny
                        ? Json(HttpStatusCode.Forbidden, new { error = "denied" })
                        : Json(HttpStatusCode.OK, new { accessToken = "access-token", refreshToken = "refresh-token" });
                }

                if (snapshot.Method == HttpMethod.Get && snapshot.PathAndQuery == "/api/v1/auth/me")
                {
                    Assert.That(snapshot.AuthorizationParameter, Is.EqualTo("access-token"));
                    Assert.That(_pollCount, Is.EqualTo(1));
                    return Json(HttpStatusCode.OK, new UserDto
                    {
                        Id = Guid.Parse("0190a000-0000-7000-8000-000000000023"),
                        Username = "browser",
                        Email = "browser@example.test",
                    });
                }

                if (snapshot.Method == HttpMethod.Post && snapshot.PathAndQuery == "/api/v1/auth/logout?refreshToken=refresh-token")
                {
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("Unexpected request: " + snapshot.PathAndQuery),
                };
            }

            private static HttpResponseMessage Json(HttpStatusCode statusCode, object payload)
            {
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload, JsonOptions),
                        Encoding.UTF8,
                        "application/json"),
                };
            }
        }

    }
}
