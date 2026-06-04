// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Represents one durable ordered file-tree mutation for synchronization clients.</summary>
    [Table("sync_changes")]
    [Index(nameof(OwnerId), nameof(Revision), IsUnique = true)]
    public class SyncChange : BaseOwnedEntity
    {
        /// <summary>Monotonic server cursor used by clients to resume remote change reads.</summary>
        [Column("revision")]
        public long Revision { get; set; }

        /// <summary>Mutation kind.</summary>
        [Column("kind")]
        public SyncChangeKind Kind { get; set; }

        /// <summary>Layout tree that contains the changed entity.</summary>
        [Column("layout_id")]
        public Guid LayoutId { get; set; }

        /// <summary>Changed file or folder identifier. The mutation kind determines the entity type.</summary>
        [Column("item_id")]
        public Guid ItemId { get; set; }

        /// <summary>Parent folder identifier after the mutation, or the previous parent for delete events.</summary>
        [Column("parent_node_id")]
        public Guid ParentNodeId { get; set; }

        /// <summary>Previous parent folder identifier for move events.</summary>
        [Column("previous_parent_node_id")]
        public Guid? PreviousParentNodeId { get; set; }

        /// <summary>Immutable file manifest identifier for file content-bearing mutations.</summary>
        [Column("file_manifest_id")]
        public Guid? FileManifestId { get; set; }

        /// <summary>Display name captured at the time of the mutation.</summary>
        [Column("name")]
        public string Name { get; set; } = null!;
    }
}
