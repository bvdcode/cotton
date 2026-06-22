// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
using System.Text.Json.Serialization;

namespace Cotton.Files
{
    /// <summary>
    /// Represents the restore outcome API payload.
    /// </summary>
    public class RestoreOutcomeDto
    {
        /// <summary>
        /// Outcome of the restore attempt.
        /// </summary>
        public RestoreStatus Status { get; set; }

        /// <summary>
        /// Path of the original parent folder, when the outcome relates to its location.
        /// </summary>
        public string? OriginalParentPath { get; set; }

        /// <summary>
        /// Path of the parent that is missing, when <see cref="Status"/> is <see cref="RestoreStatus.ParentMissing"/>.
        /// </summary>
        public string? MissingPath { get; set; }

        /// <summary>
        /// Kind of the conflicting node, when <see cref="Status"/> is <see cref="RestoreStatus.Conflict"/>.
        /// </summary>
        public RestoreConflictKind? ConflictKind { get; set; }

        /// <summary>
        /// Name of the conflicting node, when <see cref="Status"/> is <see cref="RestoreStatus.Conflict"/>.
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
        /// Human-readable explanation of why the restore did not fully succeed, when applicable.
        /// </summary>
        public string? Reason { get; set; }
    }
}
