// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>
    /// Describes how aggressively Cotton should treat storage capacity.
    /// </summary>
    public enum StorageSpaceMode
    {
        /// <summary>
        /// Prefer the normal storage policy.
        /// </summary>
        Optimal = 0,
        /// <summary>
        /// Treat storage as limited.
        /// </summary>
        Limited = 1,
        /// <summary>
        /// Treat storage as effectively unlimited.
        /// </summary>
        Unlimited = 2,
    }
}
