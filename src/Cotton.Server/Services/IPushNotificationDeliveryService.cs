// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Notifications;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Defines server-side remote push notification delivery.
    /// </summary>
    public interface IPushNotificationDeliveryService
    {
        /// <summary>
        /// Sends one remote push notification.
        /// </summary>
        Task<PushNotificationDeliveryResult> SendAsync(
            PushNotificationDeliveryRequest request,
            CancellationToken cancellationToken);
    }
}
