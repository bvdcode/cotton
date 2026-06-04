// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync.Tests.State;

public sealed class SqliteSyncStateStoreTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-sync-tests", Guid.NewGuid().ToString("N"));
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
    public async Task LoadPairAsync_ReturnsEmptyListForNewDatabase()
    {
        var store = CreateStore();
        await store.InitializeAsync();

        IReadOnlyList<SyncStateEntry> entries = await store.LoadPairAsync("pair-a");

        Assert.That(entries, Is.Empty);
    }

    [Test]
    public async Task GetChangeCursorAsync_ReturnsDefaultCursorForNewPair()
    {
        var store = CreateStore();
        await store.InitializeAsync();

        SyncChangeCursor cursor = await store.GetChangeCursorAsync("pair-a");

        Assert.Multiple(() =>
        {
            Assert.That(cursor.SyncPairId, Is.EqualTo("pair-a"));
            Assert.That(cursor.LastCursor, Is.Zero);
            Assert.That(cursor.CursorExpired, Is.False);
            Assert.That(cursor.EarliestAvailableCursor, Is.Null);
            Assert.That(cursor.UpdatedAtUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
        });
    }

    [Test]
    public async Task SaveChangeCursorAsync_RoundtripsAndPersistsAfterReopen()
    {
        string databasePath = DatabasePath();
        var first = new SqliteSyncStateStore(databasePath);
        await first.InitializeAsync();
        var updatedAt = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc);
        await first.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = "pair-a",
            LastCursor = 42,
            CursorExpired = true,
            EarliestAvailableCursor = 40,
            UpdatedAtUtc = updatedAt,
        });

        var second = new SqliteSyncStateStore(databasePath);
        await second.InitializeAsync();
        SyncChangeCursor cursor = await second.GetChangeCursorAsync("pair-a");

        Assert.Multiple(() =>
        {
            Assert.That(cursor.SyncPairId, Is.EqualTo("pair-a"));
            Assert.That(cursor.LastCursor, Is.EqualTo(42));
            Assert.That(cursor.CursorExpired, Is.True);
            Assert.That(cursor.EarliestAvailableCursor, Is.EqualTo(40));
            Assert.That(cursor.UpdatedAtUtc, Is.EqualTo(updatedAt));
        });
    }

    [Test]
    public async Task SaveChangeCursorAsync_UpdatesExistingPairOnly()
    {
        var store = CreateStore();
        await store.InitializeAsync();
        await store.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = "pair-a",
            LastCursor = 1,
        });
        await store.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = "pair-b",
            LastCursor = 7,
        });

        await store.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = "pair-a",
            LastCursor = 2,
            EarliestAvailableCursor = 1,
        });

        SyncChangeCursor pairA = await store.GetChangeCursorAsync("pair-a");
        SyncChangeCursor pairB = await store.GetChangeCursorAsync("pair-b");

        Assert.Multiple(() =>
        {
            Assert.That(pairA.LastCursor, Is.EqualTo(2));
            Assert.That(pairA.EarliestAvailableCursor, Is.EqualTo(1));
            Assert.That(pairB.LastCursor, Is.EqualTo(7));
            Assert.That(pairB.EarliestAvailableCursor, Is.Null);
        });
    }

    [Test]
    public async Task UpsertAsync_RoundtripsAndPersistsAfterReopen()
    {
        string databasePath = DatabasePath();
        var first = new SqliteSyncStateStore(databasePath);
        await first.InitializeAsync();
        Guid fileId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        await first.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "Docs/Report.txt",
            Kind = SyncEntryKind.File,
            LocalContentHash = "local-hash",
            LocalLastWriteUtc = new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc),
            RemoteNodeId = nodeId,
            RemoteFileId = fileId,
            RemoteContentHash = "remote-hash",
            RemoteETag = "sha256-remote-hash",
            SyncedAtUtc = new DateTime(2026, 6, 2, 12, 1, 0, DateTimeKind.Utc),
        });

        var second = new SqliteSyncStateStore(databasePath);
        await second.InitializeAsync();
        SyncStateEntry? entry = await second.GetAsync("pair-a", "docs/report.TXT");

        Assert.Multiple(() =>
        {
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.RelativePath, Is.EqualTo("Docs/Report.txt"));
            Assert.That(entry.Kind, Is.EqualTo(SyncEntryKind.File));
            Assert.That(entry.LocalContentHash, Is.EqualTo("local-hash"));
            Assert.That(entry.LocalLastWriteUtc, Is.EqualTo(new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc)));
            Assert.That(entry.RemoteNodeId, Is.EqualTo(nodeId));
            Assert.That(entry.RemoteFileId, Is.EqualTo(fileId));
            Assert.That(entry.RemoteETag, Is.EqualTo("sha256-remote-hash"));
        });
    }

    [Test]
    public async Task UpsertAsync_RoundtripsDirectoryEntry()
    {
        string databasePath = DatabasePath();
        Guid nodeId = Guid.NewGuid();
        var first = new SqliteSyncStateStore(databasePath);
        await first.InitializeAsync();
        await first.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "Docs/Empty",
            Kind = SyncEntryKind.Directory,
            RemoteNodeId = nodeId,
            SyncedAtUtc = new DateTime(2026, 6, 4, 10, 0, 0, DateTimeKind.Utc),
        });

        var second = new SqliteSyncStateStore(databasePath);
        await second.InitializeAsync();
        SyncStateEntry? entry = await second.GetAsync("pair-a", "docs/empty");

        Assert.Multiple(() =>
        {
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Kind, Is.EqualTo(SyncEntryKind.Directory));
            Assert.That(entry.RelativePath, Is.EqualTo("Docs/Empty"));
            Assert.That(entry.RemoteNodeId, Is.EqualTo(nodeId));
            Assert.That(entry.RemoteFileId, Is.Null);
            Assert.That(entry.LocalContentHash, Is.Null);
            Assert.That(entry.RemoteContentHash, Is.Null);
        });
    }

    [Test]
    public async Task UpsertAsync_UsesCaseInsensitivePathKeyWithinPair()
    {
        var store = CreateStore();
        await store.InitializeAsync();
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "Folder/File.txt",
            Kind = SyncEntryKind.File,
            LocalContentHash = "first",
        });
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = @"folder\file.TXT",
            Kind = SyncEntryKind.File,
            LocalContentHash = "second",
        });

        IReadOnlyList<SyncStateEntry> entries = await store.LoadPairAsync("pair-a");
        SyncStateEntry? entry = await store.GetAsync("pair-a", "FOLDER/file.txt");

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.RelativePath, Is.EqualTo("folder/file.TXT"));
            Assert.That(entry.LocalContentHash, Is.EqualTo("second"));
        });
    }

    [Test]
    public async Task ReplacePairAsync_ReplacesOnlyRequestedPair()
    {
        var store = CreateStore();
        await store.InitializeAsync();
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "old.txt",
            Kind = SyncEntryKind.File,
        });
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-b",
            RelativePath = "keep.txt",
            Kind = SyncEntryKind.File,
        });

        await store.ReplacePairAsync("pair-a", new[]
        {
            new SyncStateEntry
            {
                RelativePath = "new.txt",
                Kind = SyncEntryKind.File,
                LocalContentHash = "new",
            },
        });

        IReadOnlyList<SyncStateEntry> pairA = await store.LoadPairAsync("pair-a");
        IReadOnlyList<SyncStateEntry> pairB = await store.LoadPairAsync("pair-b");

        Assert.Multiple(() =>
        {
            Assert.That(pairA.Select(x => x.RelativePath), Is.EqualTo(new[] { "new.txt" }));
            Assert.That(pairB.Select(x => x.RelativePath), Is.EqualTo(new[] { "keep.txt" }));
        });
    }

    [Test]
    public async Task InitializeAsync_SerializesConcurrentStoresSharingDatabase()
    {
        string databasePath = DatabasePath();
        Task[] migrations = Enumerable.Range(0, 12)
            .Select(_ => new SqliteSyncStateStore(databasePath).InitializeAsync())
            .ToArray();

        await Task.WhenAll(migrations);

        var store = new SqliteSyncStateStore(databasePath);
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "ready.txt",
            Kind = SyncEntryKind.File,
            LocalContentHash = "hash",
        });

        SyncStateEntry? entry = await store.GetAsync("pair-a", "READY.txt");

        Assert.That(entry?.LocalContentHash, Is.EqualTo("hash"));
    }

    [Test]
    public async Task DeleteAsync_RemovesOneEntryOnly()
    {
        var store = CreateStore();
        await store.InitializeAsync();
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "delete.txt",
            Kind = SyncEntryKind.File,
        });
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "keep.txt",
            Kind = SyncEntryKind.File,
        });

        await store.DeleteAsync("pair-a", "DELETE.txt");

        IReadOnlyList<SyncStateEntry> entries = await store.LoadPairAsync("pair-a");
        Assert.That(entries.Select(x => x.RelativePath), Is.EqualTo(new[] { "keep.txt" }));
    }

    [Test]
    public async Task DeletePairAsync_RemovesEntriesAndCursorForRequestedPairOnly()
    {
        var store = CreateStore();
        await store.InitializeAsync();
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = "delete.txt",
            Kind = SyncEntryKind.File,
        });
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-b",
            RelativePath = "keep.txt",
            Kind = SyncEntryKind.File,
        });
        await store.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = "pair-a",
            LastCursor = 10,
        });
        await store.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = "pair-b",
            LastCursor = 20,
        });

        await store.DeletePairAsync("pair-a");

        IReadOnlyList<SyncStateEntry> pairA = await store.LoadPairAsync("pair-a");
        IReadOnlyList<SyncStateEntry> pairB = await store.LoadPairAsync("pair-b");
        SyncChangeCursor pairACursor = await store.GetChangeCursorAsync("pair-a");
        SyncChangeCursor pairBCursor = await store.GetChangeCursorAsync("pair-b");

        Assert.Multiple(() =>
        {
            Assert.That(pairA, Is.Empty);
            Assert.That(pairB.Select(x => x.RelativePath), Is.EqualTo(new[] { "keep.txt" }));
            Assert.That(pairACursor.LastCursor, Is.Zero);
            Assert.That(pairBCursor.LastCursor, Is.EqualTo(20));
        });
    }

    private SqliteSyncStateStore CreateStore()
    {
        return new SqliteSyncStateStore(DatabasePath());
    }

    private string DatabasePath()
    {
        return Path.Combine(_tempDirectory, "sync-state.sqlite");
    }
}
