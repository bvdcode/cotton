// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal user projection for master-key startup probes.
    /// </summary>
    [Table("users")]
    internal class MasterKeyProbeUser
    {
        /// <summary>
        /// Row id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Encrypted TOTP secret bytes.
        /// </summary>
        [Column("totp_secret_encrypted")]
        public byte[]? TotpSecretEncrypted { get; set; }

        /// <summary>
        /// Encrypted avatar storage hash bytes.
        /// </summary>
        [Column("avatar_hash_encrypted")]
        public byte[]? AvatarHashEncrypted { get; set; }
    }
}
