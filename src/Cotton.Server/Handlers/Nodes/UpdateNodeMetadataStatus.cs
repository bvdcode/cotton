// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Describes the outcome of updating node metadata.
    /// </summary>
    public enum UpdateNodeMetadataStatus
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
        /// The requested node was not found.
        /// </summary>
        NodeNotFound,
    }
}
