// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("layouts")]
    public class Layout : BaseOwnedEntity
    {
        [Column("is_active")]
        public bool IsActive { get; set; }
        public virtual ICollection<Node> Nodes { get; set; } = [];
    }
}
