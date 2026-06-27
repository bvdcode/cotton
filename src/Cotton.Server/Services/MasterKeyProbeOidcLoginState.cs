// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal OIDC login-state projection for master-key startup probes.
    /// </summary>
    [Table("oidc_login_states")]
    internal class MasterKeyProbeOidcLoginState
    {
        /// <summary>
        /// Row id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Raw encrypted PKCE code verifier.
        /// </summary>
        [Column("code_verifier_encrypted")]
        public string? CodeVerifierEncrypted { get; set; }

        /// <summary>
        /// Raw encrypted OIDC nonce.
        /// </summary>
        [Column("nonce_encrypted")]
        public string? NonceEncrypted { get; set; }
    }
}
