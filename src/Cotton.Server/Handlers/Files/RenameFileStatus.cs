// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Describes the outcome of renaming a file.
    /// </summary>
    public enum RenameFileStatus
    {
        /// <summary>
        /// The file was renamed.
        /// </summary>
        Renamed,

        /// <summary>
        /// The requested name is invalid.
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
