// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Lists the supported restore status values.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RestoreStatus
    {
        /// <summary>
        /// Represents the restored option.
        /// </summary>
        Restored = 0,
        /// <summary>
        /// Represents the parent missing option.
        /// </summary>
        ParentMissing = 1,
        /// <summary>
        /// Represents the conflict option.
        /// </summary>
        Conflict = 2,
        /// <summary>
        /// Represents the not restorable option.
        /// </summary>
        NotRestorable = 3,
    }

    /// <summary>
    /// Lists the supported restore conflict kind values.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RestoreConflictKind
    {
        /// <summary>
        /// Represents the folder option.
        /// </summary>
        Folder = 0,
        /// <summary>
        /// Represents the file option.
        /// </summary>
        File = 1,
    }

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
        /// Restores d node.
        /// </summary>
        public NodeDto? RestoredNode { get; set; }
        /// <summary>
        /// Restores d file.
        /// </summary>
        public NodeFileManifestDto? RestoredFile { get; set; }
        /// <summary>
        /// Gets or sets reason.
        /// </summary>
        public string? Reason { get; set; }
    }
}
