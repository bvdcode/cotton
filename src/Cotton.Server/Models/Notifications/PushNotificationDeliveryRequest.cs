// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Represents one remote push delivery attempt.
    /// </summary>
    public class PushNotificationDeliveryRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PushNotificationDeliveryRequest" /> class.
        /// </summary>
        public PushNotificationDeliveryRequest(string providerToken, PushNotificationPayloadPlan payload)
        {
            ProviderToken = providerToken;
            Payload = payload;
        }

        /// <summary>
        /// Gets the push provider registration token.
        /// </summary>
        public string ProviderToken { get; }

        /// <summary>
        /// Gets the privacy-safe payload plan.
        /// </summary>
        public PushNotificationPayloadPlan Payload { get; }
    }
}
