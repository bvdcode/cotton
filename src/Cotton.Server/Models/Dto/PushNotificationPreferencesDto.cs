// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents resolved push notification preferences for the current user.
    /// </summary>
    public class PushNotificationPreferencesDto
    {
        /// <summary>
        /// Gets or sets whether shared-file push notifications are enabled.
        /// </summary>
        public bool SharedFile { get; set; }

        /// <summary>
        /// Gets or sets whether access-request push notifications are enabled.
        /// </summary>
        public bool AccessRequest { get; set; }

        /// <summary>
        /// Gets or sets whether comment or mention push notifications are enabled.
        /// </summary>
        public bool CommentMention { get; set; }

        /// <summary>
        /// Gets or sets whether security and session push notifications are enabled.
        /// </summary>
        public bool SecuritySession { get; set; }
    }
}
