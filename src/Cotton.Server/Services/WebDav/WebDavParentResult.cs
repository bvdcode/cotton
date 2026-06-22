// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.WebDav
{
    /// <summary>
    /// Result of getting parent node for a path
    /// </summary>
    public record WebDavParentResult
    {
        /// <summary>
        /// Gets or sets whether the WebDAV resource was found.
        /// </summary>
        public bool Found { get; init; }
        /// <summary>
        /// Gets or sets the resolved WebDAV parent node.
        /// </summary>
        public Node? ParentNode { get; init; }
        /// <summary>
        /// Gets or sets the final WebDAV path segment.
        /// </summary>
        public string? ResourceName { get; init; }
    }
}
