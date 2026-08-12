// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Describes the outcome of updating file metadata.
    /// </summary>
    public enum UpdateFileMetadataStatus
    {
        /// <summary>
        /// The metadata was updated.
        /// </summary>
        Updated,

        /// <summary>
        /// The metadata patch is invalid.
        /// </summary>
        InvalidPatch,

        /// <summary>
        /// The requested file was not found.
        /// </summary>
        FileNotFound,
    }
}
