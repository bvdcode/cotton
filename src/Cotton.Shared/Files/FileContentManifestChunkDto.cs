// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Files
{
    /// <summary>
    /// Describes one ordered plaintext chunk inside an immutable file content manifest.
    /// </summary>
    public class FileContentManifestChunkDto
    {
        /// <summary>
        /// Gets or sets the zero-based chunk index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the plaintext byte offset where this chunk starts in the file.
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// Gets or sets the plaintext chunk length in bytes.
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// Gets or sets the lowercase hexadecimal SHA-256 chunk hash.
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage chunk identifier. For content-addressed Cotton chunks this matches <see cref="Hash" />.
        /// </summary>
        public string ChunkId { get; set; } = string.Empty;
    }
}
