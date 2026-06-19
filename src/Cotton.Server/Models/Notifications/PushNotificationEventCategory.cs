// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Defines server-side remote push notification categories understood by mobile clients.
    /// </summary>
    public enum PushNotificationEventCategory
    {
        /// <summary>
        /// Shared-file activity.
        /// </summary>
        SharedFile = 0,

        /// <summary>
        /// Access request activity.
        /// </summary>
        AccessRequest = 1,

        /// <summary>
        /// Comment or mention activity.
        /// </summary>
        CommentMention = 2,

        /// <summary>
        /// Security and session activity.
        /// </summary>
        SecuritySession = 3,
    }
}
