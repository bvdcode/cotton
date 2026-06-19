// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Resolves remote push event categories from stored notification template metadata.
    /// </summary>
    public static class PushNotificationEventCategoryResolver
    {
        private static readonly HashSet<string> SecurityTitleKeys = new(StringComparer.Ordinal)
        {
            NotificationTemplateKeys.FailedLoginAttemptTitle,
            NotificationTemplateKeys.SuccessfulLoginTitle,
            NotificationTemplateKeys.OtpDisabledTitle,
            NotificationTemplateKeys.OtpEnabledTitle,
            NotificationTemplateKeys.TotpFailedAttemptTitle,
            NotificationTemplateKeys.TotpLockoutTitle,
            NotificationTemplateKeys.WebDavTokenResetTitle,
            NotificationTemplateKeys.AppCodeApprovalTitle,
        };

        /// <summary>
        /// Resolves the remote push category, if this notification has one.
        /// </summary>
        public static PushNotificationEventCategory? Resolve(IReadOnlyDictionary<string, string>? metadata)
        {
            if (metadata is null
                || !metadata.TryGetValue(NotificationTemplateMetadata.TitleKey, out string? titleKey)
                || string.IsNullOrWhiteSpace(titleKey))
            {
                return null;
            }

            if (string.Equals(titleKey, NotificationTemplateKeys.SharedFileDownloadedTitle, StringComparison.Ordinal))
            {
                return PushNotificationEventCategory.SharedFile;
            }

            if (SecurityTitleKeys.Contains(titleKey))
            {
                return PushNotificationEventCategory.SecuritySession;
            }

            return null;
        }

        /// <summary>
        /// Adds remote push category metadata when the notification maps to a mobile category.
        /// </summary>
        public static void Enrich(Dictionary<string, string> metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            PushNotificationEventCategory? category = Resolve(metadata);
            if (!category.HasValue)
            {
                return;
            }

            metadata[PushNotificationMetadataKeys.EventCategory] = category.Value.ToString();
            metadata[PushNotificationMetadataKeys.VisiblePayloadPolicy] =
                PushNotificationMetadataKeys.GenericVisiblePayloadPolicy;
        }
    }
}
