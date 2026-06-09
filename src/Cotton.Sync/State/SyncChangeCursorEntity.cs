// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Sync.State
{
    /// <summary>
    /// Represents one persisted remote change-feed checkpoint row.
    /// </summary>
    [Table("sync_change_cursors")]
    public class SyncChangeCursorEntity
    {
        /// <summary>
        /// Gets or sets the sync pair identifier.
        /// </summary>
        [Key]
        [MaxLength(256)]
        [Column("sync_pair_id")]
        public string SyncPairId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last remote change cursor that was accepted by the client.
        /// </summary>
        [Column("last_cursor")]
        public long LastCursor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the saved cursor is older than the server retention window.
        /// </summary>
        [Column("cursor_expired")]
        public bool CursorExpired { get; set; }

        /// <summary>
        /// Gets or sets the earliest cursor the server can still replay when the saved cursor expired.
        /// </summary>
        [Column("earliest_available_cursor")]
        public long? EarliestAvailableCursor { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the cursor was updated.
        /// </summary>
        [Column("updated_at_utc")]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
