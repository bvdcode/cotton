// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Contracts.Sync
{
    /// <summary>
    /// Represents a page from the durable synchronization change feed.
    /// </summary>
    public class SyncChangesResponseDto
    {
        /// <summary>Cursor supplied by the caller.</summary>
        public long SinceCursor { get; set; }

        /// <summary>Cursor clients should persist after applying this response.</summary>
        public long NextCursor { get; set; }

        /// <summary>Whether more changes are available after <see cref="NextCursor"/>.</summary>
        public bool HasMore { get; set; }

        /// <summary>Whether the requested cursor is older than the retained change range.</summary>
        public bool CursorExpired { get; set; }

        /// <summary>Lowest cursor that can still be used without missing retained changes.</summary>
        public long? EarliestAvailableCursor { get; set; }

        /// <summary>Ordered change page.</summary>
        public List<SyncChangeDto> Changes { get; set; } = [];
    }
}
