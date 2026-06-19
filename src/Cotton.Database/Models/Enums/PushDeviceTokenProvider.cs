// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>
    /// Defines push notification providers supported by registered mobile devices.
    /// </summary>
    public enum PushDeviceTokenProvider
    {
        /// <summary>
        /// Firebase Cloud Messaging token.
        /// </summary>
        FirebaseCloudMessaging = 0,
    }
}
