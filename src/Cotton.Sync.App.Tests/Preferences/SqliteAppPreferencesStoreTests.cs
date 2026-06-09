// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.Preferences
{

    public sealed class SqliteAppPreferencesStoreTests
    {
        private string _tempDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-sync-app-tests", Guid.NewGuid().ToString("N"));
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
        public async Task GetAsync_ReturnsDefaultsForNewDatabase()
        {
            SqliteAppPreferencesStore store = CreateStore();
            await store.InitializeAsync();

            AppPreferences preferences = await store.GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(preferences.RememberedServerUrl, Is.Null);
                Assert.That(preferences.RememberedUsername, Is.Null);
                Assert.That(preferences.StartWithOperatingSystem, Is.True);
                Assert.That(preferences.StartMinimizedToTray, Is.False);
                Assert.That(preferences.EnableNotifications, Is.True);
                Assert.That(preferences.ThemeMode, Is.EqualTo(AppThemeMode.Dark));
            });
        }

        [Test]
        public async Task SaveAsync_RoundtripsAfterReopen()
        {
            string databasePath = DatabasePath();
            var firstStore = new SqliteAppPreferencesStore(databasePath);
            await firstStore.InitializeAsync();
            var expected = new AppPreferences
            {
                RememberedServerUrl = new Uri("https://cotton.example.test/"),
                RememberedUsername = "desktop@example.test",
                StartWithOperatingSystem = true,
                StartMinimizedToTray = true,
                EnableNotifications = false,
                ThemeMode = AppThemeMode.Dark,
                CreatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 6, 3, 10, 1, 0, DateTimeKind.Utc),
            };

            await firstStore.SaveAsync(expected);

            var secondStore = new SqliteAppPreferencesStore(databasePath);
            await secondStore.InitializeAsync();
            AppPreferences actual = await secondStore.GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(actual.RememberedServerUrl, Is.EqualTo(expected.RememberedServerUrl));
                Assert.That(actual.RememberedUsername, Is.EqualTo(expected.RememberedUsername));
                Assert.That(actual.StartWithOperatingSystem, Is.True);
                Assert.That(actual.StartMinimizedToTray, Is.True);
                Assert.That(actual.EnableNotifications, Is.False);
                Assert.That(actual.ThemeMode, Is.EqualTo(AppThemeMode.Dark));
                Assert.That(actual.CreatedAtUtc, Is.EqualTo(expected.CreatedAtUtc));
                Assert.That(actual.UpdatedAtUtc, Is.EqualTo(expected.UpdatedAtUtc));
            });
        }

        [Test]
        public async Task SaveAsync_RejectsRelativeServerUrl()
        {
            SqliteAppPreferencesStore store = CreateStore();
            await store.InitializeAsync();
            var preferences = new AppPreferences
            {
                RememberedServerUrl = new Uri("/cotton", UriKind.Relative),
            };

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await store.SaveAsync(preferences));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.ParamName, Is.EqualTo("serverUrl"));
            });
        }

        [Test]
        public async Task StoresSyncPairsAndPreferencesInSameDatabase()
        {
            string databasePath = DatabasePath();
            var syncPairStore = new SqliteSyncPairSettingsStore(databasePath);
            var preferencesStore = new SqliteAppPreferencesStore(databasePath);
            SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
            await syncPairStore.InitializeAsync();
            await syncPairStore.UpsertAsync(syncPair);
            await preferencesStore.InitializeAsync();

            await preferencesStore.SaveAsync(new AppPreferences
            {
                RememberedServerUrl = new Uri("https://cotton.example.test/"),
            });

            SyncPairSettings? persistedSyncPair = await syncPairStore.GetAsync(syncPair.Id);
            AppPreferences persistedPreferences = await preferencesStore.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(persistedSyncPair, Is.Not.Null);
                Assert.That(persistedPreferences.RememberedServerUrl, Is.EqualTo(new Uri("https://cotton.example.test/")));
            });
        }

        [Test]
        public async Task InitializeAsync_SerializesConcurrentStoresSharingDatabase()
        {
            string databasePath = DatabasePath();
            Task[] migrations = Enumerable.Range(0, 12)
                .Select(index => index % 2 == 0
                    ? new SqliteAppPreferencesStore(databasePath).InitializeAsync()
                    : new SqliteSyncPairSettingsStore(databasePath).InitializeAsync())
                .ToArray();

            await Task.WhenAll(migrations);

            var syncPairStore = new SqliteSyncPairSettingsStore(databasePath);
            var preferencesStore = new SqliteAppPreferencesStore(databasePath);
            SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
            await syncPairStore.UpsertAsync(syncPair);
            await preferencesStore.SaveAsync(new AppPreferences
            {
                RememberedServerUrl = new Uri("https://cotton.example.test/"),
            });

            SyncPairSettings? persistedSyncPair = await syncPairStore.GetAsync(syncPair.Id);
            AppPreferences persistedPreferences = await preferencesStore.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(persistedSyncPair, Is.Not.Null);
                Assert.That(persistedPreferences.RememberedServerUrl, Is.EqualTo(new Uri("https://cotton.example.test/")));
            });
        }

        [Test]
        public async Task SaveAsync_TrimsRememberedUsername()
        {
            SqliteAppPreferencesStore store = CreateStore();
            await store.InitializeAsync();

            await store.SaveAsync(new AppPreferences
            {
                RememberedUsername = "  desktop@example.test  ",
            });

            AppPreferences preferences = await store.GetAsync();

            Assert.That(preferences.RememberedUsername, Is.EqualTo("desktop@example.test"));
        }

        private SqliteAppPreferencesStore CreateStore()
        {
            return new SqliteAppPreferencesStore(DatabasePath());
        }

        private string DatabasePath()
        {
            return Path.Combine(_tempDirectory, "settings.sqlite");
        }

        private static SyncPairSettings CreatePair(string localRootPath)
        {
            return new SyncPairSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = "Documents",
                LocalRootPath = localRootPath,
                RemoteRootNodeId = Guid.NewGuid(),
                RemoteDisplayPath = "/Documents",
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
            };
        }
    }
}
