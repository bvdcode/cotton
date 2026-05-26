// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Links a Cotton user to one external OpenID Connect subject.</summary>
    [Table("user_external_identities")]
    [Index(nameof(ProviderId), nameof(Subject), IsUnique = true)]
    [Index(nameof(UserId), nameof(ProviderId), IsUnique = true)]
    public class UserExternalIdentity : BaseEntity<Guid>
    {
        /// <summary>Cotton user id owning this external identity.</summary>
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>Configured provider id.</summary>
        [Column("provider_id")]
        public Guid ProviderId { get; set; }

        /// <summary>Issuer observed and validated during linking.</summary>
        [Column("issuer")]
        [MaxLength(512)]
        public string Issuer { get; set; } = null!;

        /// <summary>Provider-local subject claim. This is the only stable external account key.</summary>
        [Column("subject")]
        [MaxLength(256)]
        public string Subject { get; set; } = null!;

        /// <summary>Latest email claim reported by the provider.</summary>
        [Column("email")]
        [MaxLength(320)]
        public string? Email { get; set; }

        /// <summary>Whether the latest provider email claim was verified.</summary>
        [Column("email_verified")]
        public bool EmailVerified { get; set; }

        /// <summary>Latest display name reported by the provider.</summary>
        [Column("display_name")]
        [MaxLength(160)]
        public string? DisplayName { get; set; }

        /// <summary>Latest provider avatar URL.</summary>
        [Column("picture_url")]
        [MaxLength(2048)]
        public string? PictureUrl { get; set; }

        /// <summary>UTC timestamp when the identity was last used to sign in.</summary>
        [Column("last_used_at")]
        public DateTime? LastUsedAt { get; set; }

        /// <summary>User navigation property.</summary>
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;

        /// <summary>Provider navigation property.</summary>
        [ForeignKey(nameof(ProviderId))]
        public virtual OidcProvider Provider { get; set; } = null!;
    }
}
