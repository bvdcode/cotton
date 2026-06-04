// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents a page of durable sync changes after a cursor.
    /// </summary>
    public class SyncChangesResponseDto
    {
        /// <summary>
        /// Gets or sets the cursor supplied by the client.
        /// </summary>
        public long SinceCursor { get; set; }
        /// <summary>
        /// Gets or sets the cursor the client should persist after this response.
        /// </summary>
        public long NextCursor { get; set; }
        /// <summary>
        /// Gets or sets whether more changes are available after <see cref="NextCursor"/>.
        /// </summary>
        public bool HasMore { get; set; }
        /// <summary>
        /// Gets or sets the returned changes.
        /// </summary>
        public List<SyncChangeDto> Changes { get; set; } = [];
    }
}
