// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Describes the outcome of resolving shared file content.
    /// </summary>
    public enum GetSharedNodeFileContentStatus
    {
        /// <summary>
        /// The requested file can be served.
        /// </summary>
        Success,

        /// <summary>
        /// The public share token was not found or is no longer active.
        /// </summary>
        SharedFolderNotFound,

        /// <summary>
        /// The requested file was not found inside the shared subtree.
        /// </summary>
        FileNotFound,
    }
}
