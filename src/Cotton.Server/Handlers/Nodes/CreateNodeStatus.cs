// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Describes the outcome of creating a node.
    /// </summary>
    public enum CreateNodeStatus
    {
        /// <summary>
        /// The node was created.
        /// </summary>
        Created,

        /// <summary>
        /// The requested name is invalid.
        /// </summary>
        InvalidName,

        /// <summary>
        /// The requested parent node was not found.
        /// </summary>
        ParentNotFound,

        /// <summary>
        /// The requested name is already used in the parent folder.
        /// </summary>
        NameConflict,
    }
}
