// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Represents one durable ordered file-tree mutation for synchronization clients.</summary>
    [Table("sync_changes")]
    [Index(nameof(OwnerId), nameof(Revision), IsUnique = true)]
    [Index(nameof(OwnerId), nameof(LayoutId), nameof(Revision))]
    [Index(nameof(OwnerId), nameof(NodeId))]
    [Index(nameof(OwnerId), nameof(NodeFileId))]
    public class SyncChange : BaseOwnedEntity
    {
        /// <summary>Monotonic server cursor used by clients to resume remote change reads.</summary>
        [Column("revision")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Revision { get; set; }

        /// <summary>Mutation kind.</summary>
        [Column("kind")]
        public SyncChangeKind Kind { get; set; }

        /// <summary>Layout tree that contains the changed entity when known.</summary>
        [Column("layout_id")]
        public Guid? LayoutId { get; set; }

        /// <summary>Changed folder identifier, or parent folder identifier for a file when appropriate.</summary>
        [Column("node_id")]
        public Guid? NodeId { get; set; }

        /// <summary>Changed file entry identifier when the mutation targets a file.</summary>
        [Column("node_file_id")]
        public Guid? NodeFileId { get; set; }

        /// <summary>Current parent folder identifier when available.</summary>
        [Column("parent_node_id")]
        public Guid? ParentNodeId { get; set; }

        /// <summary>Previous parent folder identifier for move/delete events when available.</summary>
        [Column("previous_parent_node_id")]
        public Guid? PreviousParentNodeId { get; set; }

        /// <summary>Current immutable file manifest identifier for file mutations when available.</summary>
        [Column("file_manifest_id")]
        public Guid? FileManifestId { get; set; }

        /// <summary>Original file lineage identifier for file mutations when available.</summary>
        [Column("original_node_file_id")]
        public Guid? OriginalNodeFileId { get; set; }

        /// <summary>Current display name when available.</summary>
        [Column("name")]
        [MaxLength(512)]
        public string? Name { get; set; }

        /// <summary>Current lowercase hexadecimal full-content hash for file mutations when available.</summary>
        [Column("content_hash")]
        [MaxLength(128)]
        public string? ContentHash { get; set; }

        /// <summary>Current strong content ETag for file mutations when available.</summary>
        [Column("e_tag")]
        [MaxLength(256)]
        public string? ETag { get; set; }

        /// <summary>Current content size in bytes for file mutations when available.</summary>
        [Column("size_bytes")]
        public long? SizeBytes { get; set; }

        /// <summary>Navigation property for the owning user.</summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public new virtual User Owner { get; set; } = null!;
    }
}
