// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Describes the outcome of updating file content.
    /// </summary>
    public enum UpdateFileContentStatus
    {
        /// <summary>
        /// The file content was updated.
        /// </summary>
        Updated,

        /// <summary>
        /// The requested file name is invalid.
        /// </summary>
        InvalidName,

        /// <summary>
        /// The requested file was not found.
        /// </summary>
        FileNotFound,

        /// <summary>
        /// The requested name is already used in the target folder.
        /// </summary>
        NameConflict,
    }
}
