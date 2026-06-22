// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.WebDav
{
    /// <summary>
    /// Result of resolving a WebDAV path
    /// </summary>
    public record WebDavResolveResult
    {
        /// <summary>
        /// Gets or sets whether the WebDAV resource was found.
        /// </summary>
        public bool Found { get; init; }

        /// <summary>
        /// True when the resolved resource is a collection (directory).
        /// </summary>
        public bool IsCollection { get; init; }

        /// <summary>
        /// Gets or sets the node.
        /// </summary>
        public Node? Node { get; init; }

        /// <summary>
        /// Gets or sets the node file.
        /// </summary>
        public NodeFile? NodeFile { get; init; }
    }
}
