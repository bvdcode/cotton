// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Represents one user-owned file tree such as the normal view or trash view.</summary>
    [Table("layouts")]
    public class Layout : BaseOwnedEntity
    {
        /// <summary>Whether this layout is currently active for its owner.</summary>
        [Column("is_active")]
        public bool IsActive { get; set; }
        /// <summary>Folder nodes stored by the server.</summary>
        public virtual ICollection<Node> Nodes { get; set; } = [];
    }
}
