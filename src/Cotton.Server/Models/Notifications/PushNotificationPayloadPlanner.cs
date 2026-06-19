// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Builds privacy-safe remote push payload plans for stored notifications.
    /// </summary>
    public static class PushNotificationPayloadPlanner
    {
        private const string AppTitle = "Cotton Cloud";

        /// <summary>
        /// Builds a payload plan for the supplied notification.
        /// </summary>
        public static PushNotificationPayloadPlan Create(
            Notification notification,
            IReadOnlyDictionary<string, string>? userPreferences)
        {
            ArgumentNullException.ThrowIfNull(notification);

            PushNotificationEventCategory? category =
                PushNotificationEventCategoryResolver.Resolve(notification.Metadata);
            if (!category.HasValue)
            {
                return PushNotificationPayloadPlan.NotEligible("Notification category is not eligible for remote push.");
            }

            if (notification.Id == Guid.Empty)
            {
                return PushNotificationPayloadPlan.NotEligible("Notification id is required for remote push.");
            }

            PushNotificationPreferenceSnapshot preferences =
                PushNotificationPreferencePolicy.Resolve(userPreferences);
            if (!preferences.IsEnabled(category.Value))
            {
                return PushNotificationPayloadPlan.NotEligible("User push notification preference is disabled.");
            }

            Dictionary<string, string> data = new(StringComparer.Ordinal)
            {
                [PushNotificationMetadataKeys.NotificationId] = notification.Id.ToString("D"),
                [PushNotificationMetadataKeys.DataEventCategory] = category.Value.ToString(),
                [PushNotificationMetadataKeys.Priority] = notification.Priority.ToString(),
            };

            AddTemplateKey(
                data,
                PushNotificationMetadataKeys.TitleKey,
                notification.Metadata,
                NotificationTemplateMetadata.TitleKey);
            AddTemplateKey(
                data,
                PushNotificationMetadataKeys.ContentKey,
                notification.Metadata,
                NotificationTemplateMetadata.ContentKey);

            IReadOnlyDictionary<string, string> filteredData =
                PushNotificationPayloadPrivacyPolicy.GenericVisiblePayloads.FilterData(data);

            return PushNotificationPayloadPlan.Eligible(
                category.Value,
                AppTitle,
                GetGenericBody(category.Value),
                filteredData);
        }

        private static void AddTemplateKey(
            Dictionary<string, string> data,
            string dataKey,
            IReadOnlyDictionary<string, string>? metadata,
            string metadataKey)
        {
            if (metadata is not null
                && metadata.TryGetValue(metadataKey, out string? value)
                && !string.IsNullOrWhiteSpace(value))
            {
                data[dataKey] = value;
            }
        }

        private static string GetGenericBody(PushNotificationEventCategory category)
        {
            return category switch
            {
                PushNotificationEventCategory.SharedFile => "Shared-file activity needs attention.",
                PushNotificationEventCategory.AccessRequest => "An access request needs attention.",
                PushNotificationEventCategory.CommentMention => "A comment or mention needs attention.",
                PushNotificationEventCategory.SecuritySession => "Security activity needs attention.",
                _ => "Cotton activity needs attention.",
            };
        }
    }
}
