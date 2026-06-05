// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Settings
{
    /// <summary>
    /// Represents settings needed by Cotton clients before file transfer operations.
    /// </summary>
    public class ClientSettingsDto
    {
        /// <summary>
        /// Gets or sets the server application version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the maximum accepted upload chunk size in bytes.
        /// </summary>
        public int MaxChunkSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the server-supported content hash algorithm name.
        /// </summary>
        public string SupportedHashAlgorithm { get; set; } = string.Empty;
    }
}
