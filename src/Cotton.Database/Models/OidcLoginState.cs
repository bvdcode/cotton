// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Attributes;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Short-lived state for an OpenID Connect authorization-code flow.
    /// </summary>
    [Table("oidc_login_states")]
    [Index(nameof(StateHash), IsUnique = true)]
    [Index(nameof(ExpiresAt))]
    public class OidcLoginState : BaseEntity<Guid>
    {
        /// <summary>
        /// Configured provider id.
        /// </summary>
        [Column("provider_id")]
        public Guid ProviderId { get; set; }

        /// <summary>
        /// SHA-256 hash of the opaque state sent through the browser.
        /// </summary>
        [Column("state_hash")]
        [MaxLength(64)]
        public string StateHash { get; set; } = null!;

        /// <summary>
        /// PKCE code verifier used to redeem the authorization code.
        /// </summary>
        [Encrypted]
        [Column("code_verifier_encrypted")]
        public string CodeVerifierEncrypted { get; set; } = null!;

        /// <summary>
        /// Nonce expected in the ID token.
        /// </summary>
        [Encrypted]
        [Column("nonce_encrypted")]
        public string NonceEncrypted { get; set; } = null!;

        /// <summary>
        /// Relative application URL to return to after login.
        /// </summary>
        [Column("return_url")]
        [MaxLength(1024)]
        public string ReturnUrl { get; set; } = "/";

        /// <summary>
        /// User id that requested account linking, or null for sign-in.
        /// </summary>
        [Column("link_user_id")]
        public Guid? LinkUserId { get; set; }

        /// <summary>
        /// Whether the created refresh session should be trusted.
        /// </summary>
        [Column("trust_device")]
        public bool TrustDevice { get; set; }

        /// <summary>
        /// UTC timestamp when this login state expires.
        /// </summary>
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Provider navigation property.
        /// </summary>
        [ForeignKey(nameof(ProviderId))]
        public virtual OidcProvider Provider { get; set; } = null!;
    }
}
