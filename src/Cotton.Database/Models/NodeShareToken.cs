// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Represents a share token for a folder (node).
    /// Parallel to <see cref="DownloadToken"/> which is used for file sharing.
    /// </summary>
    [Index(nameof(Token), IsUnique = true)]
    [Table("node_share_tokens")]
    public class NodeShareToken : BaseEntity<Guid>
    {
        /// <summary>
        /// Display name of the shared folder at the time of sharing.
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Unique opaque token used in the share URL.
        /// </summary>
        [Column("token")]
        public string Token { get; set; } = null!;

        /// <summary>
        /// The folder (node) being shared.
        /// </summary>
        [Column("node_id")]
        public Guid NodeId { get; set; }

        /// <summary>
        /// When the share link expires. Null means never expires.
        /// </summary>
        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// The user who created this share link.
        /// </summary>
        [Column("created_by_user_id")]
        public Guid CreatedByUserId { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User CreatedByUser { get; set; } = null!;

        [DeleteBehavior(DeleteBehavior.Cascade)]
        public virtual Node Node { get; set; } = null!;
    }
}
