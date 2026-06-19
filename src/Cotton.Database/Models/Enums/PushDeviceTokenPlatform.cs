// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>
    /// Defines mobile platforms that can receive push notifications.
    /// </summary>
    public enum PushDeviceTokenPlatform
    {
        /// <summary>
        /// Android device token.
        /// </summary>
        Android = 0,
        /// <summary>
        /// iOS device token.
        /// </summary>
        Ios = 1,
    }
}
