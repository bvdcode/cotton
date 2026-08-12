// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Notifications
{
    /// <summary>
    /// Represents unread notifications created after a cursor and the next stream position.
    /// </summary>
    public class CottonNotificationBatchDto
    {
        /// <summary>
        /// Gets or sets the newest unread notification details, capped by the requested limit.
        /// </summary>
        public IReadOnlyList<CottonNotificationDto> UnreadNotifications { get; set; } = [];

        /// <summary>
        /// Gets or sets the exact number of unread notifications created after the supplied cursor.
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// Gets or sets the newest observed notification position, including notifications read elsewhere.
        /// </summary>
        public CottonNotificationCursorDto? NextCursor { get; set; }
    }
}
