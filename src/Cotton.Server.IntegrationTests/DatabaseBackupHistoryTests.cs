// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseBackupHistoryTests
    {
        private InMemoryStorage _storage = null!;
        private readonly DatabaseBackupKeyProvider _keys = new(new CottonEncryptionSettings { MasterEncryptionKey = "history-test" });
        private DatabaseBackupManifestService Manifests => new(_storage, _keys, NullLogger<DatabaseBackupManifestService>.Instance);

        [SetUp]
        public void SetUp() => _storage = new InMemoryStorage();

        [TestCase(1, 100, 100)]
        [TestCase(168, 10, 3)]
        [TestCase(24, 10, 8)]
        public void Retention_PreservesMinimumCountAndEntireWeek(int hoursBetween, int count, int expected)
        {
            DateTime now = DateTime.UtcNow;
            BackupManifestReference[] backups = Enumerable.Range(0, count).Select(index =>
                BackupManifestReference.FromManifest(index.ToString(), CreateManifest(index.ToString(), [1], now.AddHours(-index * hoursBetween))))
                .ToArray();
            BackupManifestReference[] retained = DatabaseBackupRetention.Retain(backups.Reverse().Concat(backups), now);
            Assert.That(retained, Is.EqualTo(backups.Take(expected)));
        }

        [Test]
        public async Task Publish_PreservesExistingSchemaOneBackup_AndProtectsAllGenerations()
        {
            BackupManifest old = CreateManifest("old", [1], DateTime.UtcNow.AddDays(-14));
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(old, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            string key = Hasher.ToHexStringHash(Hasher.HashData(bytes));
            await _storage.WriteAsync(key, new MemoryStream(bytes));
            BackupManifestPointer legacy = new(1, DatabaseBackupKeyProvider.ManifestPointerLogicalKey, old.CreatedAtUtc, key, old.BackupId);
            await _storage.WriteAsync(_keys.GetScopedPointerStorageKey(),
                new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
            ResolvedBackupManifest latest = await Manifests.PublishAsync(CreateManifest("new", [2], DateTime.UtcNow));
            Assert.That(latest.Pointer.History!.Select(reference => reference.BackupId), Is.EqualTo(new[] { "new", "old" }));
            await using CottonDbContext db = new(new DbContextOptionsBuilder<CottonDbContext>().Options);
            ChunkUsageService usage = new(db, _storage, Manifests, _keys, NullLogger<ChunkUsageService>.Instance);
            HashSet<string> protectedKeys = await usage.GetProtectedStorageKeysAsync(default);
            Assert.That(protectedKeys, Is.SupersetOf(new[]
            {
                key, latest.ManifestStorageKey, old.Chunks[0].StorageKey, latest.Manifest.Chunks[0].StorageKey,
                _keys.GetScopedPointerStorageKey()
            }));
        }

        [TestCase("missing-manifest")]
        [TestCase("corrupt-manifest")]
        [TestCase("missing-chunk")]
        [TestCase("corrupt-chunk")]
        [TestCase("wrong-size")]
        public async Task Restore_SkipsInvalidNewestBackup_AndRebuildsPrevious(string failure)
        {
            await PublishAsync("older", [1, 2, 3], DateTime.UtcNow.AddDays(-1));
            BackupManifest newest = CreateManifest("newer", [4, 5, 6, 7], DateTime.UtcNow);
            if (failure == "wrong-size")
            {
                newest = newest with { DumpSizeBytes = 5, Chunks = [new BackupChunkInfo(0, newest.Chunks[0].StorageKey, 5)] };
            }
            await _storage.WriteAsync(newest.Chunks[0].StorageKey, new MemoryStream([4, 5, 6, 7]));
            ResolvedBackupManifest latest = await Manifests.PublishAsync(newest);
            switch (failure)
            {
                case "missing-manifest": await _storage.DeleteAsync(latest.ManifestStorageKey); break;
                case "corrupt-manifest": await _storage.WriteAsync(latest.ManifestStorageKey, new MemoryStream([9])); break;
                case "missing-chunk": await _storage.DeleteAsync(newest.Chunks[0].StorageKey); break;
                case "corrupt-chunk": await _storage.WriteAsync(newest.Chunks[0].StorageKey, new MemoryStream([9])); break;
                case "wrong-size": break;
                default: throw new ArgumentOutOfRangeException(nameof(failure));
            }
            string path = Path.Combine(Path.GetTempPath(), $"backup-restore-{Guid.NewGuid():N}.dump");
            try
            {
                DatabaseBackupRestorePreparation preparation = new(Manifests, _storage, NullLogger<DatabaseBackupRestorePreparation>.Instance);
                PreparedDatabaseBackup result = await preparation.PrepareAsync((await Manifests.ReadPointerAsync())!, path, default);
                Assert.That(result.Backup.Manifest.BackupId, Is.EqualTo("older"));
                Assert.That(result.SkippedBackupIds, Is.EqualTo(new[] { "newer" }));
                Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(new byte[] { 1, 2, 3 }));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task Restore_StopsIfAllBackupsAreUnavailable()
        {
            ResolvedBackupManifest backup = await PublishAsync("unavailable", [8], DateTime.UtcNow);
            await _storage.DeleteAsync(backup.ManifestStorageKey);
            DatabaseBackupRestorePreparation preparation = new(Manifests, _storage, NullLogger<DatabaseBackupRestorePreparation>.Instance);
            Assert.ThrowsAsync<InvalidOperationException>(() => preparation.PrepareAsync(backup.Pointer, "unused.dump", default));
        }

        [Test]
        public async Task Restore_PropagatesCancellation()
        {
            ResolvedBackupManifest backup = await PublishAsync("canceled", [8], DateTime.UtcNow);
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();
            DatabaseBackupRestorePreparation preparation = new(Manifests, _storage, NullLogger<DatabaseBackupRestorePreparation>.Instance);
            Assert.CatchAsync<OperationCanceledException>(() => preparation.PrepareAsync(backup.Pointer, "unused.dump", cancellation.Token));
        }

        [Test]
        public async Task Gate_SerializesOperations_AndCancellationDoesNotReleaseAnOccupiedGate()
        {
            using DatabaseBackupGate gate = new();
            await gate.WaitAsync(default);
            using CancellationTokenSource cancellation = new();
            Task canceledWait = gate.WaitAsync(cancellation.Token);
            await cancellation.CancelAsync();
            Assert.CatchAsync<OperationCanceledException>(() => canceledWait);
            Task next = gate.WaitAsync(default);
            Assert.That(next.IsCompleted, Is.False);
            gate.Release();
            await next.WaitAsync(TimeSpan.FromSeconds(5));
            gate.Release();
        }

        private async Task<ResolvedBackupManifest> PublishAsync(string id, byte[] content, DateTime created)
        {
            BackupManifest manifest = CreateManifest(id, content, created);
            await _storage.WriteAsync(manifest.Chunks[0].StorageKey, new MemoryStream(content));
            return await Manifests.PublishAsync(manifest);
        }

        private static BackupManifest CreateManifest(string id, byte[] content, DateTime created)
        {
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
            return new BackupManifest(1, id, created, "postgres_database_dump", "pg_dump_custom", "test", "localhost", "5432",
                Hasher.SupportedHashAlgorithm, content.Length, content.Length, hash, 1, TimeSpan.Zero,
                [new BackupChunkInfo(0, hash, content.Length)]);
        }
    }
}
