// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Defines which fields are allowed in remote push payloads.
    /// </summary>
    public class PushNotificationPayloadPrivacyPolicy
    {
        private static readonly HashSet<string> AllowedDataKeys = new(StringComparer.Ordinal)
        {
            PushNotificationMetadataKeys.NotificationId,
            PushNotificationMetadataKeys.DataEventCategory,
            PushNotificationMetadataKeys.Priority,
            PushNotificationMetadataKeys.TitleKey,
            PushNotificationMetadataKeys.ContentKey,
        };

        /// <summary>
        /// Gets the generic visible payload privacy policy.
        /// </summary>
        public static PushNotificationPayloadPrivacyPolicy GenericVisiblePayloads { get; } = new();

        /// <summary>
        /// Gets whether the supplied key may appear in FCM data.
        /// </summary>
        public bool AllowsDataKey(string key)
        {
            return AllowedDataKeys.Contains(key);
        }

        /// <summary>
        /// Filters data to the policy allow-list.
        /// </summary>
        public IReadOnlyDictionary<string, string> FilterData(IReadOnlyDictionary<string, string> data)
        {
            ArgumentNullException.ThrowIfNull(data);

            return data
                .Where(item => AllowsDataKey(item.Key))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        }
    }
}
