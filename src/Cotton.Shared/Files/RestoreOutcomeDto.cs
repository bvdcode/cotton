// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Files
{
    /// <summary>
    /// Represents the restore outcome API payload.
    /// </summary>
    public class RestoreOutcomeDto
    {
        /// <summary>
        /// Gets or sets status.
        /// </summary>
        public RestoreStatus Status { get; set; }

        /// <summary>
        /// Gets or sets original parent path.
        /// </summary>
        public string? OriginalParentPath { get; set; }

        /// <summary>
        /// Gets or sets missing path.
        /// </summary>
        public string? MissingPath { get; set; }

        /// <summary>
        /// Gets or sets conflict kind.
        /// </summary>
        public RestoreConflictKind? ConflictKind { get; set; }

        /// <summary>
        /// Gets or sets conflict name.
        /// </summary>
        public string? ConflictName { get; set; }

        /// <summary>
        /// Gets or sets the restored node.
        /// </summary>
        public NodeDto? RestoredNode { get; set; }

        /// <summary>
        /// Gets or sets the restored file.
        /// </summary>
        public NodeFileManifestDto? RestoredFile { get; set; }

        /// <summary>
        /// Gets or sets reason.
        /// </summary>
        public string? Reason { get; set; }
    }
}
