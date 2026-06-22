// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Represents a temporary direct-download token for one file.
    /// </summary>
    [Index(nameof(Token), IsUnique = true)]
    [Table("download_tokens")]
    public class DownloadToken : BaseEntity<Guid>
    {
        /// <summary>
        /// Download file name presented to clients.
        /// </summary>
        [Column("file_name")]
        public string FileName { get; set; } = null!;

        /// <summary>
        /// Opaque token value used to authorize this operation.
        /// </summary>
        [Column("token")]
        public string Token { get; set; } = null!;

        /// <summary>
        /// Identifier of the file entry referenced by this row.
        /// </summary>
        [Column("node_file_id")]
        public Guid NodeFileId { get; set; }

        /// <summary>
        /// UTC expiration timestamp for the token.
        /// </summary>
        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Identifier of the user who created the token.
        /// </summary>
        [Column("created_by_user_id")]
        public Guid CreatedByUserId { get; set; }

        /// <summary>
        /// Whether the token should be removed after a successful download.
        /// </summary>
        [Column("delete_after_use")]
        public bool DeleteAfterUse { get; set; }

        /// <summary>
        /// Navigation property for the user who created the row.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User CreatedByUser { get; set; } = null!;

        /// <summary>
        /// Navigation property for the file entry this token authorizes downloading.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual NodeFile NodeFile { get; set; } = null!;
    }
}
