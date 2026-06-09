// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{

    /// <summary>
    /// Represents local scan results keyed by normalized relative path.
    /// </summary>
    public sealed class LocalTreeLookupSnapshot
    {
        /// <summary>
        /// Gets discovered child directories by normalized relative path key.
        /// </summary>
        public Dictionary<string, LocalDirectorySnapshot> DirectoriesByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets discovered files by normalized relative path key.
        /// </summary>
        public Dictionary<string, LocalFileSnapshot> FilesByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
