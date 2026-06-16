// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;

namespace Cotton.Files
{
    /// <summary>
    /// Describes immutable file content and its ordered verification chunks.
    /// </summary>
    public class FileContentManifestDto
    {
        /// <summary>
        /// Gets or sets the visible node file identifier this manifest was requested through.
        /// </summary>
        public Guid NodeFileId { get; set; }

        /// <summary>
        /// Gets or sets the immutable file manifest identifier.
        /// </summary>
        public Guid FileManifestId { get; set; }

        /// <summary>
        /// Gets or sets the lowercase hexadecimal full-content SHA-256 hash.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the strong content ETag without quotes.
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plaintext file size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the nominal plaintext chunk size in bytes when the manifest has a regular chunk layout.
        /// </summary>
        public long? ChunkSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the ordered plaintext chunks used to verify ranges.
        /// </summary>
        public List<FileContentManifestChunkDto> Chunks { get; set; } = [];
    }
}
