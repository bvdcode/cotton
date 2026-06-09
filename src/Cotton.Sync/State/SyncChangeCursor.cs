// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State
{
    /// <summary>
    /// Represents the durable remote change-feed checkpoint for one sync pair.
    /// </summary>
    public class SyncChangeCursor
    {
        /// <summary>
        /// Gets or sets the sync pair identifier.
        /// </summary>
        public string SyncPairId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last remote change cursor that was accepted by the client.
        /// </summary>
        public long LastCursor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the saved cursor is older than the server retention window.
        /// </summary>
        public bool CursorExpired { get; set; }

        /// <summary>
        /// Gets or sets the earliest cursor the server can still replay when the saved cursor expired.
        /// </summary>
        public long? EarliestAvailableCursor { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the cursor was updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
