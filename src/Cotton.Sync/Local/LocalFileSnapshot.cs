// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{
    /// <summary>
    /// Represents one local file discovered by the sync scanner.
    /// </summary>
    public class LocalFileSnapshot
    {
        /// <summary>
        /// Gets or sets the normalized relative path.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute local file path.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the lowercase SHA-256 content hash.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the local last-write timestamp in UTC.
        /// </summary>
        public DateTime LastWriteUtc { get; set; }
    }
}
