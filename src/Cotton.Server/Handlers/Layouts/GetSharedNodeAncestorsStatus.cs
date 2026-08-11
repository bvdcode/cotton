// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Describes the outcome of loading shared node ancestors.
    /// </summary>
    public enum GetSharedNodeAncestorsStatus
    {
        /// <summary>
        /// The ancestor path was loaded.
        /// </summary>
        Success,

        /// <summary>
        /// The public share token was not found or is no longer active.
        /// </summary>
        SharedFolderNotFound,

        /// <summary>
        /// The requested folder was not found inside the shared subtree.
        /// </summary>
        FolderNotFound,

        /// <summary>
        /// The stored hierarchy cannot be traversed safely.
        /// </summary>
        InvalidHierarchy,
    }
}
