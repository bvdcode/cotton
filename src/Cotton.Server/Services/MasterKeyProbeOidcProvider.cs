// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal OIDC provider projection for master-key startup probes.
    /// </summary>
    [Table("oidc_providers")]
    internal class MasterKeyProbeOidcProvider
    {
        /// <summary>
        /// Row id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Raw encrypted client secret.
        /// </summary>
        [Column("client_secret_encrypted")]
        public string? ClientSecretEncrypted { get; set; }
    }
}
