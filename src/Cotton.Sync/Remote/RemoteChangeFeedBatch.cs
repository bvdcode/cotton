// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync;

namespace Cotton.Sync.Remote
{
    /// <summary>
    /// Represents one durable remote change-feed page read for a sync pair.
    /// </summary>
    public class RemoteChangeFeedBatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteChangeFeedBatch" /> class.
        /// </summary>
        public RemoteChangeFeedBatch(
            string syncPairId,
            long sinceCursor,
            long nextCursor,
            bool hasMore,
            bool cursorExpired,
            long? earliestAvailableCursor,
            IReadOnlyCollection<SyncChangeDto> changes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
            ArgumentNullException.ThrowIfNull(changes);
            if (sinceCursor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sinceCursor), sinceCursor, "Cursor cannot be negative.");
            }

            if (nextCursor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextCursor), nextCursor, "Cursor cannot be negative.");
            }

            if (nextCursor < sinceCursor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextCursor),
                    nextCursor,
                    "Next cursor cannot be before the requested cursor.");
            }

            if (earliestAvailableCursor < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(earliestAvailableCursor),
                    earliestAvailableCursor,
                    "Earliest available cursor cannot be negative.");
            }

            SyncPairId = syncPairId;
            SinceCursor = sinceCursor;
            NextCursor = nextCursor;
            HasMore = hasMore;
            CursorExpired = cursorExpired;
            EarliestAvailableCursor = earliestAvailableCursor;
            Changes = changes.ToArray();
            Snapshot = RemoteChangeFeedSnapshot.FromChanges(Changes);
            ValidateSnapshotCursorRange(Snapshot, sinceCursor, nextCursor);
        }

        /// <summary>
        /// Gets the sync pair identifier.
        /// </summary>
        public string SyncPairId { get; }

        /// <summary>
        /// Gets the cursor used to request this page.
        /// </summary>
        public long SinceCursor { get; }

        /// <summary>
        /// Gets the cursor that can be persisted after this page has been processed.
        /// </summary>
        public long NextCursor { get; }

        /// <summary>
        /// Gets a value indicating whether more changes are available after <see cref="NextCursor" />.
        /// </summary>
        public bool HasMore { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="SinceCursor" /> is older than the server retention window.
        /// </summary>
        public bool CursorExpired { get; }

        /// <summary>
        /// Gets the earliest cursor the server can still replay when <see cref="CursorExpired" /> is true.
        /// </summary>
        public long? EarliestAvailableCursor { get; }

        /// <summary>
        /// Gets ordered remote changes for this page.
        /// </summary>
        public IReadOnlyList<SyncChangeDto> Changes { get; }

        /// <summary>
        /// Gets a normalized summary of the changes in this page.
        /// </summary>
        public RemoteChangeFeedSnapshot Snapshot { get; }

        private static void ValidateSnapshotCursorRange(
            RemoteChangeFeedSnapshot snapshot,
            long sinceCursor,
            long nextCursor)
        {
            if (snapshot.IsEmpty)
            {
                return;
            }

            if (snapshot.FirstCursor <= sinceCursor)
            {
                throw new ArgumentException("Remote change cursors must be after the requested cursor.", nameof(snapshot));
            }

            if (snapshot.LastCursor > nextCursor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextCursor),
                    nextCursor,
                    "Next cursor cannot be before the last returned change cursor.");
            }
        }
    }
}
