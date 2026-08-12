// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Notifications
{
    /// <summary>
    /// Defines notification importance levels used by the Cotton API.
    /// </summary>
    public enum CottonNotificationPriority
    {
        /// <summary>
        /// No special priority.
        /// </summary>
        None = 0,

        /// <summary>
        /// Low priority.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium priority.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High priority.
        /// </summary>
        High = 3,
    }
}
