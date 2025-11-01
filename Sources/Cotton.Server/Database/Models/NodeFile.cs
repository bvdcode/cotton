// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("node_files")]
    [Index(nameof(NodeId), nameof(NormalizedName), IsUnique = true)]
    [Index(nameof(FileManifestId), nameof(NodeId), IsUnique = true)]
    public class NodeFile : BaseOwnedEntity
    {
        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("normalized_name")]
        public string NormalizedName { get; set; } = null!;

        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("node_id")]
        public Guid NodeId { get; set; }

        /// <summary>
        /// First ID is the first nodefile created for a version, remains the same for all subsequent updates.
        /// TODO: Set this id after entity was created.
        /// </summary>
        [Column("original_node_file_id")]
        public Guid OriginalNodeFileId { get; set; }

        public virtual FileManifest FileManifest { get; set; } = null!;
        public virtual Node Node { get; set; } = null!;
    }
}
