// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Nodes
{
    /// <summary>
    /// Counts and size of the direct children in a folder.
    /// </summary>
    public class FolderStatsDto
    {
        /// <summary>Number of direct child folders.</summary>
        public int Folders { get; init; }

        /// <summary>Number of direct child files.</summary>
        public int Files { get; init; }

        /// <summary>Number of direct child files encrypted by the client.</summary>
        public int EncryptedFiles { get; init; }

        /// <summary>Total size of direct child files in bytes.</summary>
        public long SizeBytes { get; init; }
    }
}
