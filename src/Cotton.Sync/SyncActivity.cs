// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{
    /// <summary>
    /// Represents one synchronization action or decision.
    /// </summary>
    public sealed class SyncActivity
    {
        /// <summary>
        /// Gets or sets the activity kind.
        /// </summary>
        public SyncActivityKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the normalized relative path associated with the activity.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional user-displayable details.
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the activity blocked sync until the user reviews it.
        /// </summary>
        public bool RequiresUserAction { get; set; }
    }
}
