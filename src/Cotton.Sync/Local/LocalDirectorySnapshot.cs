// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{

    /// <summary>
    /// Represents one local directory discovered by the sync scanner.
    /// </summary>
    public sealed class LocalDirectorySnapshot
    {
        /// <summary>
        /// Gets or sets the normalized relative path.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute local directory path.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;
    }
}
