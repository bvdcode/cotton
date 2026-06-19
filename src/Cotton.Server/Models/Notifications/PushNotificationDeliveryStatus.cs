// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Defines remote push provider delivery outcomes.
    /// </summary>
    public enum PushNotificationDeliveryStatus
    {
        /// <summary>
        /// The provider accepted the message.
        /// </summary>
        Sent = 0,

        /// <summary>
        /// Delivery is disabled because provider configuration is incomplete.
        /// </summary>
        NotConfigured = 1,

        /// <summary>
        /// The provider token is no longer usable.
        /// </summary>
        InvalidToken = 2,

        /// <summary>
        /// The provider rejected the request without proving the token is invalid.
        /// </summary>
        Rejected = 3,

        /// <summary>
        /// The provider reported a retryable failure.
        /// </summary>
        TransientFailure = 4,

        /// <summary>
        /// The sender failed before receiving a provider response.
        /// </summary>
        Failed = 5,
    }
}
