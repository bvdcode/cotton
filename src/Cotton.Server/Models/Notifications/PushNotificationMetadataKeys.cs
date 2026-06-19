// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Defines metadata keys used to mark notifications for remote push delivery.
    /// </summary>
    public static class PushNotificationMetadataKeys
    {
        /// <summary>
        /// Remote push event category metadata key.
        /// </summary>
        public const string EventCategory = "push.category";

        /// <summary>
        /// Visible payload privacy policy metadata key.
        /// </summary>
        public const string VisiblePayloadPolicy = "push.visiblePayloadPolicy";

        /// <summary>
        /// Generic visible payload policy name.
        /// </summary>
        public const string GenericVisiblePayloadPolicy = "generic-visible";

        /// <summary>
        /// FCM data key for notification id.
        /// </summary>
        public const string NotificationId = "notificationId";

        /// <summary>
        /// FCM data key for event category.
        /// </summary>
        public const string DataEventCategory = "eventCategory";

        /// <summary>
        /// FCM data key for notification priority.
        /// </summary>
        public const string Priority = "priority";

        /// <summary>
        /// FCM data key for title template.
        /// </summary>
        public const string TitleKey = "titleKey";

        /// <summary>
        /// FCM data key for content template.
        /// </summary>
        public const string ContentKey = "contentKey";
    }
}
