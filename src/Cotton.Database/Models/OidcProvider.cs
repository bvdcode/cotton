// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("oidc_providers")]
    [Index(nameof(Slug), IsUnique = true)]
    public class OidcProvider : BaseEntity<Guid>
    {
        [Column("name")]
        [MaxLength(80)]
        public string Name { get; set; } = null!;

        [Column("slug")]
        [MaxLength(64)]
        public string Slug { get; set; } = null!;

        [Column("issuer")]
        [MaxLength(512)]
        public string Issuer { get; set; } = null!;

        [Column("client_id")]
        [MaxLength(256)]
        public string ClientId { get; set; } = null!;

        [Column("client_secret_encrypted")]
        public string? ClientSecretEncrypted { get; set; }

        [Column("scopes")]
        public string[] Scopes { get; set; } = [];

        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        [Column("allow_account_creation")]
        public bool AllowAccountCreation { get; set; }

        [Column("require_verified_email")]
        public bool RequireVerifiedEmail { get; set; }

        [Column("default_role")]
        public UserRole DefaultRole { get; set; }

        [Column("allowed_email_domains")]
        public string[] AllowedEmailDomains { get; set; } = [];

        [Column("sync_profile")]
        public bool SyncProfile { get; set; }

        [Column("sync_avatar")]
        public bool SyncAvatar { get; set; }

        public virtual ICollection<UserExternalIdentity> UserIdentities { get; set; } = [];
    }
}
