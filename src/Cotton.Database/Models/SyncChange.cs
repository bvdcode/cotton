// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Cotton.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("sync_changes")]
    [Index(nameof(OwnerId), nameof(Id))]
    public class SyncChange : BaseOwnedEntity<long>
    {
        [Column("kind")]
        public SyncChangeKind Kind { get; set; }

        [Column("layout_id")]
        public Guid LayoutId { get; set; }

        [Column("item_id")]
        public Guid ItemId { get; set; }

        [Column("parent_node_id")]
        public Guid ParentNodeId { get; set; }

        [Column("previous_parent_node_id")]
        public Guid? PreviousParentNodeId { get; set; }

        [Column("file_manifest_id")]
        public Guid? FileManifestId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;
    }
}
