// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Describes the outcome of creating a node share link.
    /// </summary>
    public enum CreateNodeShareLinkStatus
    {
        /// <summary>
        /// The share link was created.
        /// </summary>
        Created,

        /// <summary>
        /// The requested node was not found for the user.
        /// </summary>
        NodeNotFound,

        /// <summary>
        /// The requested custom token is already in use.
        /// </summary>
        TokenConflict,
    }
}
