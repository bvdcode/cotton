// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{

    /// <summary>
    /// Represents a sync pass that was blocked by a safety condition requiring user review.
    /// </summary>
    public class SyncActionRequiredException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncActionRequiredException" /> class.
        /// </summary>
        public SyncActionRequiredException(string message)
            : base(message)
        {
        }
    }
}
