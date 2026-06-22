// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Represents a named file entry inside a node and points to immutable file content.
    /// </summary>
    [Table("node_files")]
    [Index(nameof(NodeId), nameof(NameKey))]
    [Index(nameof(FileManifestId), nameof(NodeId))]
    public class NodeFile : BaseOwnedEntity<Guid>
    {
        /// <summary>
        /// Identifier of the immutable file manifest referenced by this row.
        /// </summary>
        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        /// <summary>
        /// Identifier of the node referenced by this row.
        /// </summary>
        [Column("node_id")]
        public Guid NodeId { get; set; }

        /// <summary>
        /// First ID is the first nodefile created for a version, remains the same for all subsequent updates.
        /// </summary>
        [Column("original_node_file_id")]
        public Guid OriginalNodeFileId { get; set; }

        /// <summary>
        /// Gets the name associated with the entity.
        /// Use SetName method to set the name with validation.
        /// </summary>
        [Column("name")]
        public string Name { get; private set; } = null!;

        /// <summary>
        /// Gets the unique key associated with the name for this entity.
        /// Automatically set when using SetName method.
        /// </summary>
        [Column("name_key", TypeName = "citext")]
        public string NameKey { get; private set; } = null!;

        /// <summary>
        /// Extensible metadata associated with this row.
        /// </summary>
        [Column("metadata")]
        public Dictionary<string, string>? Metadata { get; set; } = [];

        /// <summary>
        /// Validates and assigns the display name and lookup key together.
        /// </summary>
        public void SetName(string input)
        {
            bool isValid = NameValidator.TryNormalizeAndValidate(input, out string normalized, out string error);
            if (!isValid)
            {
                throw new ArgumentException($"Invalid node name: {error}");
            }
            Name = normalized;
            NameKey = NameValidator.GetNameKey(normalized);
        }

        /// <summary>
        /// Navigation property for immutable file content.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual FileManifest FileManifest { get; set; } = null!;

        /// <summary>
        /// Navigation property for the referenced node.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Node Node { get; set; } = null!;

        /// <summary>
        /// Temporary direct-download token rows.
        /// </summary>
        public virtual ICollection<DownloadToken> DownloadTokens { get; set; } = [];
    }
}
