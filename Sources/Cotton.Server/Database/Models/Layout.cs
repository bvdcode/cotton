// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Server.Database.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("layouts")]
    public class Layout : BaseOwnedEntity
    {
        [Column("is_active")]
        public bool IsActive { get; set; }
        public virtual ICollection<Node> Nodes { get; set; } = [];
    }
}
