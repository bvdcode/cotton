// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Notifications
{
    /// <summary>
    /// Identifies a stable position in the notification stream.
    /// </summary>
    public class CottonNotificationCursorDto
    {
        /// <summary>
        /// Gets or sets the UTC creation timestamp of the notification at the cursor.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the notification identifier used to order equal timestamps.
        /// </summary>
        public Guid NotificationId { get; set; }
    }
}
