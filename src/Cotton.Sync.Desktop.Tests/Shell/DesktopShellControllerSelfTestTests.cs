// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopShellControllerSelfTestTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-shell-self-test-" + Guid.NewGuid().ToString("N"));
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
    public async Task RunSelfTestAsync_IncludesReleaseRequiredChecks()
    {
        using DesktopShellController controller = CreateController();

        DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

        string[] names = result.Items.Select(static item => item.Name).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("Preferences database"));
            Assert.That(names, Does.Contain("Sync pair database"));
            Assert.That(names, Does.Contain("Sync state database"));
            Assert.That(names, Does.Contain("Authentication state"));
            Assert.That(names, Does.Contain("File watcher"));
            Assert.That(names, Does.Contain("Server identity"));
        });
    }

    [Test]
    public async Task RunSelfTestAsync_IncludesLocalAndRemoteRootChecksForSyncPairs()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        string localRoot = Path.Combine(_tempDirectory, "Documents");
        Directory.CreateDirectory(localRoot);
        var syncPairStore = new SqliteSyncPairSettingsStore(paths.AppDatabasePath);
        await syncPairStore.InitializeAsync();
        await syncPairStore.UpsertAsync(new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Documents",
            LocalRootPath = localRoot,
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = "/Documents",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        using DesktopShellController controller = CreateController(paths, syncPairStore);

        DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(static item => item.Name), Does.Contain("Local root: Documents"));
            Assert.That(result.Items.Select(static item => item.Name), Does.Contain("Remote root: Documents"));
            Assert.That(result.Items.Single(static item => item.Name == "Remote root: Documents").Details, Is.EqualTo("Sign in to verify"));
        });
    }

    private DesktopShellController CreateController()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        return CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));
    }

    private static DesktopShellController CreateController(
        DesktopAppPaths paths,
        SqliteSyncPairSettingsStore syncPairStore)
    {
        var loggerFactory = new DesktopTraceLoggerFactory();
        return new DesktopShellController(
            paths,
            new DesktopSyncApplicationFactory(paths, loggerFactory),
            new SqliteAppPreferencesStore(paths.AppDatabasePath),
            syncPairStore,
            new FakePlatformCommandService(),
            new FakeAutostartService());
    }

    private sealed class FakeAutostartService : IAutostartService
    {
        public bool IsSupported => true;

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlatformCommandService : IPlatformCommandService
    {
        public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
