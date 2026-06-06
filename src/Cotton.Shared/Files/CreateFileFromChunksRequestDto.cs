// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;

namespace Cotton.Files
{
    /// <summary>
    /// Represents a create-file or update-file-content request based on already uploaded chunks.
    /// </summary>
    public class CreateFileFromChunksRequestDto
    {
        /// <summary>
        /// Gets or sets the target parent node identifier.
        /// </summary>
        public Guid NodeId { get; set; }

        /// <summary>
        /// Gets or sets ordered lowercase hexadecimal chunk hashes.
        /// </summary>
        public List<string> ChunkHashes { get; set; } = [];

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MIME content type.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the lowercase hexadecimal full-content hash.
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional original file identifier for version lineage creation.
        /// </summary>
        public Guid? OriginalNodeFileId { get; set; }

        /// <summary>
        /// Gets or sets structured metadata attached to the file entry.
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server should validate reconstructed content hash.
        /// </summary>
        public bool Validate { get; set; }
    }
}
