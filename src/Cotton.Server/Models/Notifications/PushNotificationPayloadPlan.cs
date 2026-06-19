// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Represents a privacy-safe remote push payload plan for a stored notification.
    /// </summary>
    public class PushNotificationPayloadPlan
    {
        private PushNotificationPayloadPlan(
            bool isEligible,
            PushNotificationEventCategory? category,
            string? title,
            string? body,
            IReadOnlyDictionary<string, string> data,
            string? skipReason)
        {
            IsEligible = isEligible;
            Category = category;
            Title = title;
            Body = body;
            Data = data;
            SkipReason = skipReason;
        }

        /// <summary>
        /// Gets whether this notification is eligible for remote push delivery.
        /// </summary>
        public bool IsEligible { get; }

        /// <summary>
        /// Gets the remote push category.
        /// </summary>
        public PushNotificationEventCategory? Category { get; }

        /// <summary>
        /// Gets the generic visible title.
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// Gets the generic visible body.
        /// </summary>
        public string? Body { get; }

        /// <summary>
        /// Gets privacy-safe FCM data fields.
        /// </summary>
        public IReadOnlyDictionary<string, string> Data { get; }

        /// <summary>
        /// Gets the reason this notification is not eligible for remote push.
        /// </summary>
        public string? SkipReason { get; }

        /// <summary>
        /// Creates an eligible payload plan.
        /// </summary>
        public static PushNotificationPayloadPlan Eligible(
            PushNotificationEventCategory category,
            string title,
            string body,
            IReadOnlyDictionary<string, string> data)
        {
            return new PushNotificationPayloadPlan(
                true,
                category,
                title,
                body,
                new Dictionary<string, string>(data),
                null);
        }

        /// <summary>
        /// Creates an ineligible payload plan.
        /// </summary>
        public static PushNotificationPayloadPlan NotEligible(string reason)
        {
            return new PushNotificationPayloadPlan(
                false,
                null,
                null,
                null,
                new Dictionary<string, string>(),
                reason);
        }
    }
}
