// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Abstractions;
using Cotton.Server.Database.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("nodes")]
    [Index(nameof(LayoutId), nameof(ParentId), nameof(Type), nameof(NormalizedName), IsUnique = true)]
    public class Node : BaseOwnedEntity
    {
        [Column("layout_id")]
        public Guid LayoutId { get; set; }

        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("normalized_name")]
        public string NormalizedName { get; set; } = null!;

        [Column("type")]
        // TODO: make sure the parent node type is the same as this node type
        public NodeType Type { get; set; }

        public virtual Layout Layout { get; set; } = null!;
        public virtual Node? Parent { get; set; }
        public virtual ICollection<Node> Children { get; set; } = [];
        public virtual ICollection<NodeFile> LayoutNodeFiles { get; set; } = [];
    }
}
