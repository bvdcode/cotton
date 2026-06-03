// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>Represents a page from the durable synchronization change feed.</summary>
    public sealed class SyncChangesResponseDto
    {
        /// <summary>Cursor supplied by the caller.</summary>
        public long SinceCursor { get; set; }

        /// <summary>Cursor clients should persist after applying this response.</summary>
        public long NextCursor { get; set; }

        /// <summary>Whether more changes are available after <see cref="NextCursor"/>.</summary>
        public bool HasMore { get; set; }

        /// <summary>Ordered change page.</summary>
        public List<SyncChangeDto> Changes { get; set; } = [];
    }
}
