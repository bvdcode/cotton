// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Sync.Remote
{
    /// <summary>
    /// Represents one remote directory discovered during a remote tree crawl.
    /// </summary>
    public sealed class RemoteDirectorySnapshot
    {
        /// <summary>
        /// Gets or sets the normalized relative path.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the remote node DTO.
        /// </summary>
        public NodeDto Node { get; set; } = new();
    }
}
