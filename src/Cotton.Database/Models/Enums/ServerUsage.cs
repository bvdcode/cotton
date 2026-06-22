// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>
    /// Describes intended server usage for setup and diagnostics.
    /// </summary>
    public enum ServerUsage
    {
        /// <summary>
        /// General-purpose usage.
        /// </summary>
        Other = 0,
        /// <summary>
        /// Photo-focused usage.
        /// </summary>
        Photos = 1,
        /// <summary>
        /// Document-focused usage.
        /// </summary>
        Documents = 2,
        /// <summary>
        /// Media-focused usage.
        /// </summary>
        Media = 3,
    }
}
