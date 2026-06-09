// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using Cotton.Sync;
using Cotton.Models.Enums;
using Cotton.Sdk.Sync;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Tests.Remote
{
    public class RemoteChangeFeedReaderTests
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
        public async Task ReadAsync_UsesSavedCursorAndDoesNotAdvanceBeforeAcknowledge()
        {
            var stateStore = CreateStore();
            await stateStore.InitializeAsync();
            await stateStore.SaveChangeCursorAsync(new SyncChangeCursor
            {
                SyncPairId = "pair-a",
                LastCursor = 10,
            });
            var syncClient = new FakeCottonSyncClient(new SyncChangesResponseDto
            {
                SinceCursor = 10,
                NextCursor = 12,
                HasMore = true,
                Changes =
                [
                    new SyncChangeDto
                    {
                        Id = 11,
                        Kind = SyncChangeKind.FileCreated,
                        LayoutId = Guid.NewGuid(),
                        ItemId = Guid.NewGuid(),
                        ParentNodeId = Guid.NewGuid(),
                        Name = "report.txt",
                    },
                ],
            });
            var reader = new RemoteChangeFeedReader(syncClient, stateStore);

            RemoteChangeFeedBatch batch = await reader.ReadAsync("pair-a", limit: 25);
            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("pair-a");

            Assert.Multiple(() =>
            {
                Assert.That(syncClient.Requests, Is.EqualTo(new[] { (SinceCursor: 10L, Limit: 25) }));
                Assert.That(batch.SyncPairId, Is.EqualTo("pair-a"));
                Assert.That(batch.SinceCursor, Is.EqualTo(10));
                Assert.That(batch.NextCursor, Is.EqualTo(12));
                Assert.That(batch.HasMore, Is.True);
                Assert.That(batch.Changes, Has.Count.EqualTo(1));
                Assert.That(cursor.LastCursor, Is.EqualTo(10));
            });
        }

        [Test]
        public async Task ReadFromCursorAsync_UsesExplicitCursorAndDoesNotAdvanceSavedCursor()
        {
            var stateStore = CreateStore();
            await stateStore.InitializeAsync();
            await stateStore.SaveChangeCursorAsync(new SyncChangeCursor
            {
                SyncPairId = "pair-a",
                LastCursor = 10,
            });
            var syncClient = new FakeCottonSyncClient(new SyncChangesResponseDto
            {
                SinceCursor = 12,
                NextCursor = 14,
                HasMore = false,
            });
            var reader = new RemoteChangeFeedReader(syncClient, stateStore);

            RemoteChangeFeedBatch batch = await reader.ReadFromCursorAsync("pair-a", sinceCursor: 12, limit: 30);
            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("pair-a");

            Assert.Multiple(() =>
            {
                Assert.That(syncClient.Requests, Is.EqualTo(new[] { (SinceCursor: 12L, Limit: 30) }));
                Assert.That(batch.SinceCursor, Is.EqualTo(12));
                Assert.That(batch.NextCursor, Is.EqualTo(14));
                Assert.That(cursor.LastCursor, Is.EqualTo(10));
            });
        }

        [Test]
        public async Task ReadFromCursorAsync_RejectsMismatchedResponseCursor()
        {
            var stateStore = CreateStore();
            await stateStore.InitializeAsync();
            var syncClient = new FakeCottonSyncClient(new SyncChangesResponseDto
            {
                SinceCursor = 9,
                NextCursor = 10,
                HasMore = false,
            });
            var reader = new RemoteChangeFeedReader(syncClient, stateStore);

            Assert.ThrowsAsync<InvalidOperationException>(
                () => reader.ReadFromCursorAsync("pair-a", sinceCursor: 10, limit: 30));
        }

        [Test]
        public async Task AcknowledgeAsync_SavesNextCursorForProcessedBatch()
        {
            var stateStore = CreateStore();
            await stateStore.InitializeAsync();
            var reader = new RemoteChangeFeedReader(new FakeCottonSyncClient(), stateStore);
            var batch = new RemoteChangeFeedBatch(
                "pair-a",
                sinceCursor: 10,
                nextCursor: 12,
                hasMore: false,
                cursorExpired: false,
                earliestAvailableCursor: 5,
                changes: Array.Empty<SyncChangeDto>());

            await reader.AcknowledgeAsync(batch);

            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("pair-a");
            Assert.Multiple(() =>
            {
                Assert.That(cursor.LastCursor, Is.EqualTo(12));
                Assert.That(cursor.CursorExpired, Is.False);
                Assert.That(cursor.EarliestAvailableCursor, Is.EqualTo(5));
            });
        }

        [Test]
        public async Task AcknowledgeAsync_MarksExpiredCursorWithoutAdvancing()
        {
            var stateStore = CreateStore();
            await stateStore.InitializeAsync();
            await stateStore.SaveChangeCursorAsync(new SyncChangeCursor
            {
                SyncPairId = "pair-a",
                LastCursor = 10,
            });
            var reader = new RemoteChangeFeedReader(new FakeCottonSyncClient(), stateStore);
            var batch = new RemoteChangeFeedBatch(
                "pair-a",
                sinceCursor: 10,
                nextCursor: 10,
                hasMore: false,
                cursorExpired: true,
                earliestAvailableCursor: 15,
                changes: Array.Empty<SyncChangeDto>());

            await reader.AcknowledgeAsync(batch);

            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("pair-a");
            Assert.Multiple(() =>
            {
                Assert.That(cursor.LastCursor, Is.EqualTo(10));
                Assert.That(cursor.CursorExpired, Is.True);
                Assert.That(cursor.EarliestAvailableCursor, Is.EqualTo(15));
            });
        }

        [Test]
        public async Task AcknowledgeFullResyncAsync_RecoversExpiredCursorToEarliestAvailableCursor()
        {
            var stateStore = CreateStore();
            await stateStore.InitializeAsync();
            await stateStore.SaveChangeCursorAsync(new SyncChangeCursor
            {
                SyncPairId = "pair-a",
                LastCursor = 10,
                CursorExpired = true,
                EarliestAvailableCursor = 15,
            });
            var reader = new RemoteChangeFeedReader(new FakeCottonSyncClient(), stateStore);
            var batch = new RemoteChangeFeedBatch(
                "pair-a",
                sinceCursor: 10,
                nextCursor: 10,
                hasMore: false,
                cursorExpired: true,
                earliestAvailableCursor: 15,
                changes: Array.Empty<SyncChangeDto>());

            await reader.AcknowledgeFullResyncAsync(batch);

            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("pair-a");
            Assert.Multiple(() =>
            {
                Assert.That(cursor.LastCursor, Is.EqualTo(15));
                Assert.That(cursor.CursorExpired, Is.False);
                Assert.That(cursor.EarliestAvailableCursor, Is.EqualTo(15));
            });
        }

        [Test]
        public async Task ReadAsync_CatchesUpFiveThousandRemoteChangesWithinSmokeTarget()
        {
            const int pageSize = 500;
            const int pageCount = 10;
            const int expectedChangeCount = pageSize * pageCount;
            TimeSpan smokeTarget = TimeSpan.FromSeconds(10);
            var stateStore = CreateStore();
            await stateStore.InitializeAsync();
            var syncClient = new FakeCottonSyncClient(CreateChangePages(pageSize, pageCount));
            var reader = new RemoteChangeFeedReader(syncClient, stateStore);
            int totalChanges = 0;

            Stopwatch stopwatch = Stopwatch.StartNew();
            RemoteChangeFeedBatch batch;
            do
            {
                batch = await reader.ReadAsync("pair-a", pageSize);
                totalChanges += batch.Changes.Count;
                await reader.AcknowledgeAsync(batch);
            }
            while (batch.HasMore);

            stopwatch.Stop();
            SyncChangeCursor cursor = await stateStore.GetChangeCursorAsync("pair-a");
            TestContext.WriteLine(
                "Remote change cursor catch-up smoke for {0} changes completed in {1:N0} ms.",
                expectedChangeCount,
                stopwatch.Elapsed.TotalMilliseconds);

            Assert.Multiple(() =>
            {
                Assert.That(totalChanges, Is.EqualTo(expectedChangeCount));
                Assert.That(cursor.LastCursor, Is.EqualTo(expectedChangeCount));
                Assert.That(cursor.CursorExpired, Is.False);
                Assert.That(syncClient.Requests, Has.Count.EqualTo(pageCount));
                Assert.That(syncClient.Requests.Select(request => request.SinceCursor), Is.EqualTo(Enumerable.Range(0, pageCount).Select(page => (long)(page * pageSize))));
                Assert.That(syncClient.Requests.Select(request => request.Limit), Is.All.EqualTo(pageSize));
                Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
            });
        }

        private SqliteSyncStateStore CreateStore()
        {
            return new SqliteSyncStateStore(Path.Combine(_tempDirectory, "sync-state.sqlite"));
        }

        private static SyncChangesResponseDto[] CreateChangePages(int pageSize, int pageCount)
        {
            var pages = new SyncChangesResponseDto[pageCount];
            for (int page = 0; page < pageCount; page++)
            {
                long sinceCursor = page * pageSize;
                long nextCursor = sinceCursor + pageSize;
                pages[page] = new SyncChangesResponseDto
                {
                    SinceCursor = sinceCursor,
                    NextCursor = nextCursor,
                    HasMore = page < pageCount - 1,
                    Changes = Enumerable.Range(1, pageSize)
                        .Select(offset => new SyncChangeDto
                        {
                            Id = sinceCursor + offset,
                            Kind = SyncChangeKind.FileContentUpdated,
                            LayoutId = Guid.NewGuid(),
                            ItemId = Guid.NewGuid(),
                            ParentNodeId = Guid.NewGuid(),
                            Name = "file-" + (sinceCursor + offset).ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + ".txt",
                            CreatedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc),
                        })
                        .ToList(),
                };
            }

            return pages;
        }

        private class FakeCottonSyncClient : ICottonSyncClient
        {
            private readonly Queue<SyncChangesResponseDto> _responses;

            public FakeCottonSyncClient(params SyncChangesResponseDto[] responses)
            {
                _responses = new Queue<SyncChangesResponseDto>(responses);
            }

            public List<(long SinceCursor, int Limit)> Requests { get; } = [];

            public Task<SyncChangesResponseDto> GetChangesAsync(
                long sinceCursor = 0,
                int limit = 500,
                CancellationToken cancellationToken = default)
            {
                Requests.Add((sinceCursor, limit));
                return Task.FromResult(_responses.Dequeue());
            }
        }
    }
}
