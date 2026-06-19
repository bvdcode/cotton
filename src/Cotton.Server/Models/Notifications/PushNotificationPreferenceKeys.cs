// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Defines user preference keys used for remote push notification delivery.
    /// </summary>
    public static class PushNotificationPreferenceKeys
    {
        /// <summary>
        /// Shared-file push preference key.
        /// </summary>
        public const string SharedFile = "notifications.push.sharedFile";

        /// <summary>
        /// Access-request push preference key.
        /// </summary>
        public const string AccessRequest = "notifications.push.accessRequest";

        /// <summary>
        /// Comment or mention push preference key.
        /// </summary>
        public const string CommentMention = "notifications.push.commentMention";

        /// <summary>
        /// Security and session push preference key.
        /// </summary>
        public const string SecuritySession = "notifications.push.securitySession";
    }
}
