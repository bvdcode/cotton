// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>Controls the importance level of a notification.</summary>
    public enum NotificationPriority
    {
        /// <summary>Disable the feature.</summary>
        None = 0,
        /// <summary>Low priority.</summary>
        Low = 1,
        /// <summary>Medium priority.</summary>
        Medium = 2,
        /// <summary>High priority.</summary>
        High = 3,
    }
}
