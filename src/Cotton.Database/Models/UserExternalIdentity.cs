// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("user_external_identities")]
    [Index(nameof(ProviderId), nameof(Subject), IsUnique = true)]
    [Index(nameof(UserId), nameof(ProviderId), IsUnique = true)]
    public class UserExternalIdentity : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("provider_id")]
        public Guid ProviderId { get; set; }

        [Column("issuer")]
        [MaxLength(512)]
        public string Issuer { get; set; } = null!;

        [Column("subject")]
        [MaxLength(256)]
        public string Subject { get; set; } = null!;

        [Column("email")]
        [MaxLength(320)]
        public string? Email { get; set; }

        [Column("email_verified")]
        public bool EmailVerified { get; set; }

        [Column("display_name")]
        [MaxLength(160)]
        public string? DisplayName { get; set; }

        [Column("picture_url")]
        [MaxLength(2048)]
        public string? PictureUrl { get; set; }

        [Column("last_used_at")]
        public DateTime? LastUsedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User User { get; set; } = null!;

        [ForeignKey(nameof(ProviderId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual OidcProvider Provider { get; set; } = null!;
    }
}
