// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Sync.Remote
{
    /// <summary>
    /// Represents remote scan results keyed by normalized relative path.
    /// </summary>
    public class RemoteTreeLookupSnapshot
    {
        /// <summary>
        /// Gets or sets the root node used for the crawl.
        /// </summary>
        public NodeDto RootNode { get; set; } = new();

        /// <summary>
        /// Gets discovered child directories by normalized relative path key.
        /// </summary>
        public Dictionary<string, RemoteDirectorySnapshot> DirectoriesByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets discovered files by normalized relative path key.
        /// </summary>
        public Dictionary<string, RemoteFileSnapshot> FilesByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
