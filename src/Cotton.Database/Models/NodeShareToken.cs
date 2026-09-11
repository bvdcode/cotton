// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Index(nameof(Token), IsUnique = true)]
    [Table("node_share_tokens")]
    public class NodeShareToken : BaseEntity<Guid>
    {
        [Column("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Unique opaque token used in the share URL.
        /// </summary>
        [Column("token")]
        public string Token { get; set; } = null!;

        [Column("node_id")]
        public Guid NodeId { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [Column("created_by_user_id")]
        public Guid CreatedByUserId { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User CreatedByUser { get; set; } = null!;

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Node Node { get; set; } = null!;
    }
}
