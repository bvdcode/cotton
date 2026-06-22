// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Attributes;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// OpenID Connect identity provider configured by an administrator.
    /// </summary>
    [Table("oidc_providers")]
    [Index(nameof(Slug), IsUnique = true)]
    public class OidcProvider : BaseEntity<Guid>
    {
        /// <summary>
        /// Human-readable provider name displayed in the UI.
        /// </summary>
        [Column("name")]
        [MaxLength(80)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Stable URL-safe identifier used by login routes.
        /// </summary>
        [Column("slug")]
        [MaxLength(64)]
        public string Slug { get; set; } = null!;

        /// <summary>
        /// Issuer URL from the provider discovery document.
        /// </summary>
        [Column("issuer")]
        [MaxLength(512)]
        public string Issuer { get; set; } = null!;

        /// <summary>
        /// OAuth/OIDC client id.
        /// </summary>
        [Column("client_id")]
        [MaxLength(256)]
        public string ClientId { get; set; } = null!;

        /// <summary>
        /// Encrypted OAuth/OIDC client secret.
        /// </summary>
        [Encrypted]
        [Column("client_secret_encrypted")]
        public string? ClientSecretEncrypted { get; set; }

        /// <summary>
        /// Scopes requested during authorization.
        /// </summary>
        [Column("scopes")]
        public string[] Scopes { get; set; } = [];

        /// <summary>
        /// Whether users can start auth through this provider.
        /// </summary>
        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Whether unknown external identities may create Cotton accounts.
        /// </summary>
        [Column("allow_account_creation")]
        public bool AllowAccountCreation { get; set; }

        /// <summary>
        /// Whether automatic account creation requires a verified provider email.
        /// </summary>
        [Column("require_verified_email")]
        public bool RequireVerifiedEmail { get; set; }

        /// <summary>
        /// Role assigned to accounts automatically created through this provider.
        /// </summary>
        [Column("default_role")]
        public UserRole DefaultRole { get; set; }

        /// <summary>
        /// Optional email domains allowed to auto-create accounts.
        /// </summary>
        [Column("allowed_email_domains")]
        public string[] AllowedEmailDomains { get; set; } = [];

        /// <summary>
        /// Whether profile names should be synchronized from provider claims on login.
        /// </summary>
        [Column("sync_profile")]
        public bool SyncProfile { get; set; }

        /// <summary>
        /// Whether avatar URL should be synchronized from provider claims on login.
        /// </summary>
        [Column("sync_avatar")]
        public bool SyncAvatar { get; set; }

        /// <summary>
        /// User links created for this provider.
        /// </summary>
        public virtual ICollection<UserExternalIdentity> UserIdentities { get; set; } = [];
    }
}
