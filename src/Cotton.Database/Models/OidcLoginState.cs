// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Attributes;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("oidc_login_states")]
    [Index(nameof(StateHash), IsUnique = true)]
    [Index(nameof(ExpiresAt))]
    public class OidcLoginState : BaseEntity<Guid>
    {
        [Column("provider_id")]
        public Guid ProviderId { get; set; }

        /// <summary>
        /// SHA-256 hash of the opaque state sent through the browser.
        /// </summary>
        [Column("state_hash")]
        [MaxLength(64)]
        public string StateHash { get; set; } = null!;

        [Encrypted]
        [Column("code_verifier_encrypted")]
        public string CodeVerifierEncrypted { get; set; } = null!;

        [Encrypted]
        [Column("nonce_encrypted")]
        public string NonceEncrypted { get; set; } = null!;

        [Column("return_url")]
        [MaxLength(1024)]
        public string ReturnUrl { get; set; } = null!;

        [Column("link_user_id")]
        public Guid? LinkUserId { get; set; }

        [Column("trust_device")]
        public bool TrustDevice { get; set; }

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [ForeignKey(nameof(ProviderId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual OidcProvider Provider { get; set; } = null!;
    }
}
