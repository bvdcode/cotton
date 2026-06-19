// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Resolves and persists user preferences for remote push notification delivery.
    /// </summary>
    public static class PushNotificationPreferencePolicy
    {
        /// <summary>
        /// Gets the default server-side remote push preference snapshot.
        /// </summary>
        public static PushNotificationPreferenceSnapshot Default { get; } =
            new(
                sharedFile: false,
                accessRequest: false,
                commentMention: false,
                securitySession: true);

        /// <summary>
        /// Resolves push preferences from user preference key-value data.
        /// </summary>
        public static PushNotificationPreferenceSnapshot Resolve(
            IReadOnlyDictionary<string, string>? preferences)
        {
            return new PushNotificationPreferenceSnapshot(
                ReadBoolean(preferences, PushNotificationPreferenceKeys.SharedFile, Default.SharedFile),
                ReadBoolean(preferences, PushNotificationPreferenceKeys.AccessRequest, Default.AccessRequest),
                ReadBoolean(preferences, PushNotificationPreferenceKeys.CommentMention, Default.CommentMention),
                ReadBoolean(preferences, PushNotificationPreferenceKeys.SecuritySession, Default.SecuritySession));
        }

        /// <summary>
        /// Persists push preferences into user preference key-value data.
        /// </summary>
        public static void Apply(
            Dictionary<string, string> preferences,
            PushNotificationPreferenceSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(snapshot);

            preferences[PushNotificationPreferenceKeys.SharedFile] = FormatBoolean(snapshot.SharedFile);
            preferences[PushNotificationPreferenceKeys.AccessRequest] = FormatBoolean(snapshot.AccessRequest);
            preferences[PushNotificationPreferenceKeys.CommentMention] = FormatBoolean(snapshot.CommentMention);
            preferences[PushNotificationPreferenceKeys.SecuritySession] = FormatBoolean(snapshot.SecuritySession);
        }

        /// <summary>
        /// Converts a preference snapshot to an API payload.
        /// </summary>
        public static PushNotificationPreferencesDto ToDto(PushNotificationPreferenceSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return new PushNotificationPreferencesDto
            {
                SharedFile = snapshot.SharedFile,
                AccessRequest = snapshot.AccessRequest,
                CommentMention = snapshot.CommentMention,
                SecuritySession = snapshot.SecuritySession,
            };
        }

        private static bool ReadBoolean(
            IReadOnlyDictionary<string, string>? preferences,
            string key,
            bool defaultValue)
        {
            if (preferences is null
                || !preferences.TryGetValue(key, out string? value)
                || string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return bool.TryParse(value.Trim(), out bool parsed)
                ? parsed
                : defaultValue;
        }

        private static string FormatBoolean(bool value)
        {
            return value ? bool.TrueString : bool.FalseString;
        }
    }
}
