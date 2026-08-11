// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Describes the outcome of renaming a node.
    /// </summary>
    public enum RenameNodeStatus
    {
        /// <summary>
        /// The node was renamed.
        /// </summary>
        Renamed,

        /// <summary>
        /// The requested name is invalid.
        /// </summary>
        InvalidName,

        /// <summary>
        /// The requested node was not found.
        /// </summary>
        NodeNotFound,

        /// <summary>
        /// The requested name is already used in the parent folder.
        /// </summary>
        NameConflict,
    }
}
