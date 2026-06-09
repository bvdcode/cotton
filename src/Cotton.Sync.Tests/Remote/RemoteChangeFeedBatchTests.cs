// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync;
using Cotton.Models.Enums;
using Cotton.Sync.Remote;

namespace Cotton.Sync.Tests.Remote
{
    public sealed class RemoteChangeFeedBatchTests
    {
        [Test]
        public void Constructor_RejectsNextCursorBeforeSinceCursor()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RemoteChangeFeedBatch(
                    "pair-a",
                    sinceCursor: 10,
                    nextCursor: 9,
                    hasMore: false,
                    cursorExpired: false,
                    earliestAvailableCursor: null,
                    changes: Array.Empty<SyncChangeDto>()));
        }

        [Test]
        public void Constructor_RejectsChangesAtOrBeforeSinceCursor()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(
                    () => CreateBatch(sinceCursor: 10, nextCursor: 11, CreateChange(10)));

                Assert.Throws<ArgumentException>(
                    () => CreateBatch(sinceCursor: 10, nextCursor: 11, CreateChange(9)));
            });
        }

        [Test]
        public void Constructor_RejectsNextCursorBeforeLastReturnedChange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateBatch(sinceCursor: 10, nextCursor: 11, CreateChange(11), CreateChange(12)));
        }

        private static RemoteChangeFeedBatch CreateBatch(
            long sinceCursor,
            long nextCursor,
            params SyncChangeDto[] changes)
        {
            return new RemoteChangeFeedBatch(
                "pair-a",
                sinceCursor,
                nextCursor,
                hasMore: false,
                cursorExpired: false,
                earliestAvailableCursor: null,
                changes);
        }

        private static SyncChangeDto CreateChange(long id)
        {
            return new SyncChangeDto
            {
                Id = id,
                Kind = SyncChangeKind.FileContentUpdated,
                LayoutId = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                ParentNodeId = Guid.NewGuid(),
                Name = "report.txt",
            };
        }
    }
}
