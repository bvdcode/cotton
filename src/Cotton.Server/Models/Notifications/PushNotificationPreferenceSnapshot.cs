// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Represents resolved remote push notification preferences.
    /// </summary>
    public class PushNotificationPreferenceSnapshot
    {
        /// <summary>
        /// Initializes a new push notification preference snapshot.
        /// </summary>
        public PushNotificationPreferenceSnapshot(
            bool sharedFile,
            bool accessRequest,
            bool commentMention,
            bool securitySession)
        {
            SharedFile = sharedFile;
            AccessRequest = accessRequest;
            CommentMention = commentMention;
            SecuritySession = securitySession;
        }

        /// <summary>
        /// Gets whether shared-file push notifications are enabled.
        /// </summary>
        public bool SharedFile { get; }

        /// <summary>
        /// Gets whether access-request push notifications are enabled.
        /// </summary>
        public bool AccessRequest { get; }

        /// <summary>
        /// Gets whether comment or mention push notifications are enabled.
        /// </summary>
        public bool CommentMention { get; }

        /// <summary>
        /// Gets whether security and session push notifications are enabled.
        /// </summary>
        public bool SecuritySession { get; }

        /// <summary>
        /// Gets whether the supplied category is enabled for remote push delivery.
        /// </summary>
        public bool IsEnabled(PushNotificationEventCategory category)
        {
            return category switch
            {
                PushNotificationEventCategory.SharedFile => SharedFile,
                PushNotificationEventCategory.AccessRequest => AccessRequest,
                PushNotificationEventCategory.CommentMention => CommentMention,
                PushNotificationEventCategory.SecuritySession => SecuritySession,
                _ => false,
            };
        }

        /// <summary>
        /// Creates a copy with selected category values changed.
        /// </summary>
        public PushNotificationPreferenceSnapshot With(
            bool? sharedFile = null,
            bool? accessRequest = null,
            bool? commentMention = null,
            bool? securitySession = null)
        {
            return new PushNotificationPreferenceSnapshot(
                sharedFile ?? SharedFile,
                accessRequest ?? AccessRequest,
                commentMention ?? CommentMention,
                securitySession ?? SecuritySession);
        }
    }
}
