// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.App.State;

namespace Cotton.Sync.App.Tests.SyncPairs
{
    public class SqliteSyncPairSettingsStoreTests
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
        public async Task ListAsync_ReturnsEmptyListForNewDatabase()
        {
            SqliteSyncPairSettingsStore store = CreateStore();
            await store.InitializeAsync();

            IReadOnlyList<SyncPairSettings> syncPairs = await store.ListAsync();

            Assert.That(syncPairs, Is.Empty);
        }

        [Test]
        public async Task InitializeAsync_CreatesParentDirectory()
        {
            string databasePath = Path.Combine(_tempDirectory, "nested", "settings.sqlite");
            var store = new SqliteSyncPairSettingsStore(databasePath);

            await store.InitializeAsync();

            Assert.That(File.Exists(databasePath), Is.True);
        }

        [Test]
        public async Task UpsertAsync_RoundtripsAfterReopen()
        {
            string databasePath = DatabasePath();
            var firstStore = new SqliteSyncPairSettingsStore(databasePath);
            await firstStore.InitializeAsync();
            SyncPairSettings expected = CreatePair("Documents", "/home/user/Documents", "/Documents");

            await firstStore.UpsertAsync(expected);

            var secondStore = new SqliteSyncPairSettingsStore(databasePath);
            await secondStore.InitializeAsync();
            SyncPairSettings? actual = await secondStore.GetAsync(expected.Id);

            Assert.Multiple(() =>
            {
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual!.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.DisplayName, Is.EqualTo("Documents"));
                Assert.That(actual.LocalRootPath, Is.EqualTo("/home/user/Documents"));
                Assert.That(actual.RemoteRootNodeId, Is.EqualTo(expected.RemoteRootNodeId));
                Assert.That(actual.RemoteDisplayPath, Is.EqualTo("/Documents"));
                Assert.That(actual.IsEnabled, Is.True);
                Assert.That(actual.Mode, Is.EqualTo(SyncPairMode.FullMirror));
                Assert.That(actual.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(actual.UpdatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            });
        }

        [Test]
        public async Task GetAsync_NormalizesLegacyFullMirrorModeZero()
        {
            string databasePath = DatabasePath();
            Guid syncPairId = Guid.NewGuid();
            var contextFactory = new SqliteSyncAppDbContextFactory(databasePath);
            await contextFactory.MigrateAsync(CancellationToken.None);
            await using (SyncAppDbContext context = contextFactory.Create())
            {
                context.SyncPairSettings.Add(new SyncPairSettingsEntity
                {
                    Id = syncPairId,
                    DisplayName = "Documents",
                    LocalRootPath = "/home/user/Documents",
                    RemoteRootNodeId = Guid.NewGuid(),
                    RemoteDisplayPath = "/Documents",
                    IsEnabled = true,
                    Mode = SyncPairMode.Unknown,
                    CreatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
                });
                await context.SaveChangesAsync();
            }

            var store = new SqliteSyncPairSettingsStore(databasePath);
            SyncPairSettings? actual = await store.GetAsync(syncPairId);

            Assert.That(actual?.Mode, Is.EqualTo(SyncPairMode.FullMirror));
        }

        [Test]
        public async Task UpsertAsync_RejectsUnknownMode()
        {
            SqliteSyncPairSettingsStore store = CreateStore();
            await store.InitializeAsync();
            SyncPairSettings syncPair = CreatePair("Documents", "/home/user/Documents", "/Documents");
            syncPair.Mode = SyncPairMode.Unknown;

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.UpsertAsync(syncPair));
        }

        [Test]
        public async Task UpsertAsync_UpdatesExistingPair()
        {
            SqliteSyncPairSettingsStore store = CreateStore();
            await store.InitializeAsync();
            SyncPairSettings syncPair = CreatePair("Documents", "/home/user/Documents", "/Documents");
            await store.UpsertAsync(syncPair);

            syncPair.DisplayName = "Work";
            syncPair.LocalRootPath = "/home/user/Work";
            syncPair.RemoteDisplayPath = "/Work";
            syncPair.IsEnabled = false;
            await store.UpsertAsync(syncPair);

            IReadOnlyList<SyncPairSettings> syncPairs = await store.ListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(syncPairs, Has.Count.EqualTo(1));
                Assert.That(syncPairs[0].DisplayName, Is.EqualTo("Work"));
                Assert.That(syncPairs[0].LocalRootPath, Is.EqualTo("/home/user/Work"));
                Assert.That(syncPairs[0].RemoteDisplayPath, Is.EqualTo("/Work"));
                Assert.That(syncPairs[0].IsEnabled, Is.False);
            });
        }

        [Test]
        public async Task DeleteAsync_RemovesOnlyRequestedPair()
        {
            SqliteSyncPairSettingsStore store = CreateStore();
            await store.InitializeAsync();
            SyncPairSettings first = CreatePair("Documents", "/home/user/Documents", "/Documents");
            SyncPairSettings second = CreatePair("Pictures", "/home/user/Pictures", "/Pictures");
            await store.UpsertAsync(first);
            await store.UpsertAsync(second);

            await store.DeleteAsync(first.Id);

            SyncPairSettings? deleted = await store.GetAsync(first.Id);
            IReadOnlyList<SyncPairSettings> syncPairs = await store.ListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.Null);
                Assert.That(syncPairs.Select(pair => pair.Id), Is.EqualTo(new[] { second.Id }));
            });
        }

        private SqliteSyncPairSettingsStore CreateStore()
        {
            return new SqliteSyncPairSettingsStore(DatabasePath());
        }

        private string DatabasePath()
        {
            return Path.Combine(_tempDirectory, "settings.sqlite");
        }

        private static SyncPairSettings CreatePair(string displayName, string localRootPath, string remoteDisplayPath)
        {
            return new SyncPairSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = displayName,
                LocalRootPath = localRootPath,
                RemoteRootNodeId = Guid.NewGuid(),
                RemoteDisplayPath = remoteDisplayPath,
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
            };
        }
    }
}
