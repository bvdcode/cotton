// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Describes the outcome of loading node ancestors.
    /// </summary>
    public enum GetNodeAncestorsStatus
    {
        /// <summary>
        /// The ancestor path was loaded.
        /// </summary>
        Success,

        /// <summary>
        /// The requested node was not found.
        /// </summary>
        NodeNotFound,

        /// <summary>
        /// The stored hierarchy cannot be traversed safely.
        /// </summary>
        InvalidHierarchy,
    }
}
