// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Auth;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.State;

namespace Cotton.Sync.Desktop.Tests.Shell
{
    public class DesktopShellControllerSelfTestTests
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
                Assert.That(names, Does.Contain("Token storage"));
                Assert.That(names, Does.Contain("Desktop icon"));
                Assert.That(names, Does.Contain("Desktop platform"));
                Assert.That(names, Does.Contain("Tray lifecycle"));
                Assert.That(names, Does.Contain("Notification adapter"));
                Assert.That(names, Does.Contain("File watcher"));
                Assert.That(names, Does.Contain("Server identity"));
                Assert.That(names, Does.Contain("Desktop sync change feed"));
            });
        }

        [Test]
        public async Task RunSelfTestAsync_IncludesNotificationIdentityDetails()
        {
            using DesktopShellController controller = CreateController();

            DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

            DesktopSelfTestItemSnapshot item = result.Items.Single(static selfTestItem => selfTestItem.Name == "Notification adapter");
            Assert.Multiple(() =>
            {
                Assert.That(item.Passed, Is.True);
                Assert.That(item.Details, Does.Contain("adapter: "));
                Assert.That(item.Details, Does.Contain("app name: Cotton Sync"));
                Assert.That(item.Details, Does.Contain("icon: "));
            });
        }

        [Test]
        public async Task RunSelfTestAsync_FailsTokenStorageWhenProtectorIsNotReleaseSecure()
        {
            var tokenStorage = new DesktopTokenStorageCapabilitySnapshot(
                "restricted-file-v1",
                IsReleaseSecure: false,
                "Development fallback");
            using DesktopShellController controller = CreateController(tokenStorageCapabilities: () => tokenStorage);

            DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

            DesktopSelfTestItemSnapshot item = result.Items.Single(static selfTestItem => selfTestItem.Name == "Token storage");
            Assert.Multiple(() =>
            {
                Assert.That(item.Passed, Is.False);
                Assert.That(item.Details, Does.Contain("not release secure"));
                Assert.That(result.Passed, Is.False);
            });
        }

        [Test]
        public async Task RunSelfTestAsync_PassesTokenStorageWhenProtectorIsReleaseSecure()
        {
            var tokenStorage = new DesktopTokenStorageCapabilitySnapshot(
                "linux-secret-service-v1",
                IsReleaseSecure: true,
                "Linux Secret Service through secret-tool");
            using DesktopShellController controller = CreateController(tokenStorageCapabilities: () => tokenStorage);

            DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

            DesktopSelfTestItemSnapshot item = result.Items.Single(static selfTestItem => selfTestItem.Name == "Token storage");
            Assert.Multiple(() =>
            {
                Assert.That(item.Passed, Is.True);
                Assert.That(item.Details, Is.EqualTo("Linux Secret Service through secret-tool"));
            });
        }

        [Test]
        public async Task RunSelfTestAsync_VerifiesLooseDesktopIconAsset()
        {
            using DesktopShellController controller = CreateController();

            DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

            DesktopSelfTestItemSnapshot icon = result.Items.Single(static item => item.Name == "Desktop icon");
            Assert.Multiple(() =>
            {
                Assert.That(icon.Passed, Is.True);
                Assert.That(icon.Details, Does.EndWith(Path.Combine("Assets", "icon-192.png")));
            });
        }

        [Test]
        public async Task RunSelfTestAsync_VerifiesSyncStateCursorStoreAndReportsPath()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

            DesktopSelfTestItemSnapshot item = result.Items.Single(static selfTestItem => selfTestItem.Name == "Sync state database");
            var stateStore = new SqliteSyncStateStore(paths.SyncStateDatabasePath);
            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("pair-a");

            Assert.Multiple(() =>
            {
                Assert.That(item.Passed, Is.True);
                Assert.That(item.Details, Does.Contain(paths.SyncStateDatabasePath));
                Assert.That(File.Exists(paths.SyncStateDatabasePath), Is.True);
                Assert.That(cursor.LastCursor, Is.Zero);
            });
        }

        [Test]
        public async Task RunSelfTestAsync_UsesReadableFailureDetails()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            using DesktopShellController controller = CreateController(
                paths,
                new SqliteSyncPairSettingsStore(paths.AppDatabasePath),
                autostartService: new ThrowingAutostartService(
                    new InvalidOperationException("SQLite Error 1: 'no such table: sync_change_cursors'.")));

            DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync();

            DesktopSelfTestItemSnapshot item = result.Items.Single(static selfTestItem => selfTestItem.Name == "Autostart adapter");
            Assert.Multiple(() =>
            {
                Assert.That(item.Passed, Is.False);
                Assert.That(
                    item.Details,
                    Is.EqualTo("Local sync state database is unavailable. Run diagnostics and restart Cotton Sync."));
                Assert.That(item.Details, Does.Not.Contain("sync_change_cursors"));
            });
        }

        [Test]
        public async Task ExportDiagnosticsAsync_RecordsServerProbeTimeoutWithoutThrowing()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            await using var server = new SlowServerInfoEndpoint(TimeSpan.FromSeconds(5));
            var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
            await preferencesStore.InitializeAsync();
            await preferencesStore.SaveAsync(new AppPreferences
            {
                RememberedServerUrl = server.BaseAddress,
            });
            using DesktopShellController controller = CreateController(
                paths,
                new SqliteSyncPairSettingsStore(paths.AppDatabasePath),
                serverProbeTimeout: TimeSpan.FromMilliseconds(50));

            string archivePath = await controller.ExportDiagnosticsAsync();

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            string diagnosticsJson = ReadEntry(archive, "diagnostics.json");
            using JsonDocument document = JsonDocument.Parse(diagnosticsJson);
            JsonElement serverIdentity = document.RootElement
                .GetProperty("selfTestItems")
                .EnumerateArray()
                .Single(item => string.Equals(
                    item.GetProperty("name").GetString(),
                    "Server identity",
                    StringComparison.Ordinal));
            Assert.Multiple(() =>
            {
                Assert.That(serverIdentity.GetProperty("passed").GetBoolean(), Is.False);
                Assert.That(
                    serverIdentity.GetProperty("details").GetString(),
                    Is.EqualTo("Cotton server check timed out after 0.05 seconds."));
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

        [Test]
        public async Task LoadAsync_IncludesDiagnosticsFieldsForSyncPairs()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            Guid syncPairId = Guid.NewGuid();
            Guid remoteRootNodeId = Guid.NewGuid();
            DateTime lastSyncedAtUtc = new(2026, 6, 3, 12, 30, 0, DateTimeKind.Utc);
            var syncPairStore = new SqliteSyncPairSettingsStore(paths.AppDatabasePath);
            await syncPairStore.InitializeAsync();
            await syncPairStore.UpsertAsync(new SyncPairSettings
            {
                Id = syncPairId,
                DisplayName = "Documents",
                LocalRootPath = Path.Combine(_tempDirectory, "Documents"),
                RemoteRootNodeId = remoteRootNodeId,
                RemoteDisplayPath = "/Documents",
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            var stateStore = new SqliteSyncStateStore(paths.SyncStateDatabasePath);
            await stateStore.InitializeAsync();
            await stateStore.UpsertAsync(new SyncStateEntry
            {
                SyncPairId = syncPairId.ToString(),
                RelativePath = "file.txt",
                Kind = SyncEntryKind.File,
                SyncedAtUtc = lastSyncedAtUtc,
            });
            await stateStore.SaveChangeCursorAsync(new SyncChangeCursor
            {
                SyncPairId = syncPairId.ToString(),
                LastCursor = 42,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            using DesktopShellController controller = CreateController(paths, syncPairStore);

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            DesktopSyncPairSnapshot syncPair = snapshot.SyncPairs.Single();
            Assert.Multiple(() =>
            {
                Assert.That(syncPair.RemoteRootNodeId, Is.EqualTo(remoteRootNodeId));
                Assert.That(syncPair.LastSyncedAtUtc, Is.EqualTo(lastSyncedAtUtc));
                Assert.That(syncPair.ChangeCursor, Is.EqualTo(42));
                Assert.That(syncPair.LastError, Is.Null);
            });
        }

        [Test]
        public async Task LoadAsync_IncludesNotificationPreference()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
            await preferencesStore.InitializeAsync();
            AppPreferences preferences = await preferencesStore.GetAsync();
            preferences.EnableNotifications = false;
            await preferencesStore.SaveAsync(preferences);
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.That(snapshot.EnableNotifications, Is.False);
        }

        [Test]
        public async Task LoadAsync_IncludesDataPathsForDiagnostics()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.DataPaths.DataDirectory, Is.EqualTo(paths.DataDirectory));
                Assert.That(snapshot.DataPaths.AppDatabasePath, Is.EqualTo(paths.AppDatabasePath));
                Assert.That(snapshot.DataPaths.SyncStateDatabasePath, Is.EqualTo(paths.SyncStateDatabasePath));
                Assert.That(snapshot.DataPaths.TokenStorePath, Is.EqualTo(paths.TokenStorePath));
            });
        }

        [Test]
        public async Task LoadAsync_InitializesSyncStateDatabaseForNewProfile()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            await controller.LoadAsync();

            var stateStore = new SqliteSyncStateStore(paths.SyncStateDatabasePath);
            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("new-profile");
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(paths.SyncStateDatabasePath), Is.True);
                Assert.That(cursor.LastCursor, Is.Zero);
            });
        }

        [Test]
        public async Task LoadAsync_ReturnsEmptySignInHintsForNewPreferences()
        {
            using DesktopShellController controller = CreateController();

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ServerUrl, Is.Null);
                Assert.That(snapshot.RememberedUsername, Is.Null);
                Assert.That(snapshot.IsSignedIn, Is.False);
            });
        }

        [Test]
        public async Task LoadAsync_ReturnsRememberedSignInHintsWithoutStoredSession()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
            await preferencesStore.InitializeAsync();
            await preferencesStore.SaveAsync(new AppPreferences
            {
                RememberedServerUrl = new Uri("https://cotton.example.test/"),
                RememberedUsername = "desktop@example.test",
            });
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ServerUrl, Is.EqualTo(new Uri("https://cotton.example.test/")));
                Assert.That(snapshot.RememberedUsername, Is.EqualTo("desktop@example.test"));
                Assert.That(snapshot.IsSignedIn, Is.False);
                Assert.That(snapshot.AccountName, Is.Null);
            });
        }

        [Test]
        public async Task LoadAsync_IncludesThemePreference()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
            await preferencesStore.InitializeAsync();
            AppPreferences preferences = await preferencesStore.GetAsync();
            preferences.ThemeMode = AppThemeMode.Dark;
            await preferencesStore.SaveAsync(preferences);
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            DesktopShellSnapshot snapshot = await controller.LoadAsync();

            Assert.That(snapshot.ThemeMode, Is.EqualTo(AppThemeMode.Dark));
        }

        [Test]
        public async Task SetNotificationsEnabledAsync_PersistsPreference()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
            await preferencesStore.InitializeAsync();
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            await controller.SetNotificationsEnabledAsync(false);

            AppPreferences preferences = await preferencesStore.GetAsync();
            Assert.That(preferences.EnableNotifications, Is.False);
        }

        [Test]
        public async Task SetThemeModeAsync_PersistsPreference()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
            await preferencesStore.InitializeAsync();
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            await controller.SetThemeModeAsync(AppThemeMode.Light);

            AppPreferences preferences = await preferencesStore.GetAsync();
            Assert.That(preferences.ThemeMode, Is.EqualTo(AppThemeMode.Light));
        }

        [Test]
        public async Task SetSyncPairEnabledAsync_PersistsEnabledState()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var syncPairStore = new SqliteSyncPairSettingsStore(paths.AppDatabasePath);
            await syncPairStore.InitializeAsync();
            SyncPairSettings syncPair = CreateSyncPair(isEnabled: true);
            await syncPairStore.UpsertAsync(syncPair);
            using DesktopShellController controller = CreateController(paths, syncPairStore);

            await controller.SetSyncPairEnabledAsync(syncPair.Id, enabled: false);

            SyncPairSettings? persisted = await syncPairStore.GetAsync(syncPair.Id);
            Assert.Multiple(() =>
            {
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted!.IsEnabled, Is.False);
                Assert.That(persisted.UpdatedAtUtc, Is.GreaterThan(syncPair.UpdatedAtUtc));
            });
        }

        [Test]
        public async Task RenameSyncPairAsync_PersistsDisplayName()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var syncPairStore = new SqliteSyncPairSettingsStore(paths.AppDatabasePath);
            await syncPairStore.InitializeAsync();
            SyncPairSettings syncPair = CreateSyncPair(isEnabled: true);
            await syncPairStore.UpsertAsync(syncPair);
            using DesktopShellController controller = CreateController(paths, syncPairStore);

            await controller.RenameSyncPairAsync(syncPair.Id, "  Work documents  ");

            SyncPairSettings? persisted = await syncPairStore.GetAsync(syncPair.Id);
            Assert.Multiple(() =>
            {
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted!.DisplayName, Is.EqualTo("Work documents"));
                Assert.That(persisted.UpdatedAtUtc, Is.GreaterThan(syncPair.UpdatedAtUtc));
            });
        }

        [Test]
        public async Task RemoveSyncPairAsync_DeletesConfiguredPair()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var syncPairStore = new SqliteSyncPairSettingsStore(paths.AppDatabasePath);
            await syncPairStore.InitializeAsync();
            SyncPairSettings syncPair = CreateSyncPair(isEnabled: true);
            await syncPairStore.UpsertAsync(syncPair);
            var stateStore = new SqliteSyncStateStore(paths.SyncStateDatabasePath);
            await stateStore.InitializeAsync();
            await stateStore.UpsertAsync(new SyncStateEntry
            {
                SyncPairId = syncPair.Id.ToString(),
                RelativePath = "synced.txt",
                Kind = SyncEntryKind.File,
            });
            await stateStore.SaveChangeCursorAsync(new SyncChangeCursor
            {
                SyncPairId = syncPair.Id.ToString(),
                LastCursor = 42,
            });
            using DesktopShellController controller = CreateController(paths, syncPairStore);

            await controller.RemoveSyncPairAsync(syncPair.Id);

            SyncPairSettings? persisted = await syncPairStore.GetAsync(syncPair.Id);
            IReadOnlyList<SyncStateEntry> entries = await stateStore.LoadPairAsync(syncPair.Id.ToString());
            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync(syncPair.Id.ToString());

            Assert.Multiple(() =>
            {
                Assert.That(persisted, Is.Null);
                Assert.That(entries, Is.Empty);
                Assert.That(cursor.LastCursor, Is.Zero);
            });
        }

        [Test]
        public async Task ExportDiagnosticsAsync_UsesInformationalAppVersion()
        {
            using DesktopShellController controller = CreateController();

            string archivePath = await controller.ExportDiagnosticsAsync();

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            string diagnosticsJson = ReadEntry(archive, "diagnostics.json");
            using JsonDocument document = JsonDocument.Parse(diagnosticsJson);
            Assert.That(document.RootElement.GetProperty("appVersion").GetString(), Is.EqualTo(DesktopAppVersion.Current));
        }

        [Test]
        public async Task ExportDiagnosticsAsync_IncludesDataPathMetadata()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            using DesktopShellController controller = CreateController(paths, new SqliteSyncPairSettingsStore(paths.AppDatabasePath));

            string archivePath = await controller.ExportDiagnosticsAsync();

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            string diagnosticsJson = ReadEntry(archive, "diagnostics.json");
            using JsonDocument document = JsonDocument.Parse(diagnosticsJson);
            JsonElement dataPaths = document.RootElement.GetProperty("dataPaths");

            Assert.Multiple(() =>
            {
                Assert.That(dataPaths.GetProperty("dataDirectory").GetString(), Is.EqualTo(paths.DataDirectory));
                Assert.That(dataPaths.GetProperty("appDatabasePath").GetString(), Is.EqualTo(paths.AppDatabasePath));
                Assert.That(dataPaths.GetProperty("syncStateDatabasePath").GetString(), Is.EqualTo(paths.SyncStateDatabasePath));
                Assert.That(dataPaths.GetProperty("tokenStorePath").GetString(), Is.EqualTo(paths.TokenStorePath));
            });
        }

        private DesktopShellController CreateController(
            Func<DesktopTokenStorageCapabilitySnapshot>? tokenStorageCapabilities = null)
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            return CreateController(
                paths,
                new SqliteSyncPairSettingsStore(paths.AppDatabasePath),
                tokenStorageCapabilities);
        }

        private static DesktopShellController CreateController(
            DesktopAppPaths paths,
            SqliteSyncPairSettingsStore syncPairStore,
            Func<DesktopTokenStorageCapabilitySnapshot>? tokenStorageCapabilities = null,
            IAutostartService? autostartService = null,
            TimeSpan? serverProbeTimeout = null)
        {
            var loggerFactory = new DesktopTraceLoggerFactory();
            return new DesktopShellController(
                paths,
                new DesktopSyncApplicationFactory(paths, loggerFactory),
                new SqliteAppPreferencesStore(paths.AppDatabasePath),
                syncPairStore,
                new FakePlatformCommandService(),
                autostartService ?? new FakeAutostartService(),
                tokenStorageCapabilities: tokenStorageCapabilities,
                serverProbeTimeout: serverProbeTimeout);
        }

        private SyncPairSettings CreateSyncPair(bool isEnabled)
        {
            return new SyncPairSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = "Documents",
                LocalRootPath = Path.Combine(_tempDirectory, "Documents"),
                RemoteRootNodeId = Guid.NewGuid(),
                RemoteDisplayPath = "/Documents",
                IsEnabled = isEnabled,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            };
        }

        private static string ReadEntry(ZipArchive archive, string name)
        {
            ZipArchiveEntry entry = archive.GetEntry(name) ?? throw new InvalidOperationException(name + " was not found.");
            using Stream stream = entry.Open();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private sealed class SlowServerInfoEndpoint : IAsyncDisposable
        {
            private readonly HttpListener _listener = new();
            private readonly CancellationTokenSource _cancellation = new();
            private readonly TimeSpan _delay;
            private readonly Task _listenTask;

            public SlowServerInfoEndpoint(TimeSpan delay)
            {
                _delay = delay;
                BaseAddress = new Uri("http://127.0.0.1:" + GetFreePort().ToString(System.Globalization.CultureInfo.InvariantCulture) + "/");
                _listener.Prefixes.Add(BaseAddress.AbsoluteUri);
                _listener.Start();
                _listenTask = Task.Run(HandleOneRequestAsync);
            }

            public Uri BaseAddress { get; }

            public async ValueTask DisposeAsync()
            {
                _cancellation.Cancel();
                _listener.Close();
                try
                {
                    await _listenTask.ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException or OperationCanceledException)
                {
                }

                _cancellation.Dispose();
            }

            private async Task HandleOneRequestAsync()
            {
                HttpListenerContext context = await _listener.GetContextAsync().WaitAsync(_cancellation.Token)
                    .ConfigureAwait(false);
                await Task.Delay(_delay, _cancellation.Token).ConfigureAwait(false);
                byte[] payload = Encoding.UTF8.GetBytes("{\"product\":\"Cotton Cloud\",\"instanceIdHash\":\"test\"}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload, _cancellation.Token).ConfigureAwait(false);
                context.Response.Close();
            }

            private static int GetFreePort()
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
        }

        private class FakeAutostartService : IAutostartService
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

        private class ThrowingAutostartService : IAutostartService
        {
            private readonly Exception _exception;

            public ThrowingAutostartService(Exception exception)
            {
                _exception = exception;
            }

            public bool IsSupported => true;

            public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
            {
                throw _exception;
            }

            public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
            {
                throw _exception;
            }
        }

        private class FakePlatformCommandService : IPlatformCommandService
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
}
