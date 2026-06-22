// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Stores one WebAuthn/passkey credential registered by a user.
    /// </summary>
    [Table("user_passkey_credentials")]
    [Index(nameof(CredentialId), IsUnique = true)]
    [Index(nameof(UserId))]
    public class UserPasskeyCredential : BaseEntity<Guid>
    {
        /// <summary>
        /// Identifier of the user associated with this row.
        /// </summary>
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>
        /// WebAuthn credential identifier.
        /// </summary>
        [Column("credential_id")]
        public byte[] CredentialId { get; set; } = [];

        /// <summary>
        /// COSE public key for verifying passkey assertions.
        /// </summary>
        [Column("public_key")]
        public byte[] PublicKey { get; set; } = [];

        /// <summary>
        /// WebAuthn user handle bound to the credential.
        /// </summary>
        [Column("user_handle")]
        public byte[] UserHandle { get; set; } = [];

        /// <summary>
        /// Authenticator signature counter used for cloned-credential detection.
        /// </summary>
        [Column("signature_counter")]
        public long SignatureCounter { get; set; }

        /// <summary>
        /// Human-readable name displayed to users.
        /// </summary>
        [Column("name")]
        [MaxLength(120)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Authenticator transports reported during passkey registration.
        /// </summary>
        [Column("transports")]
        public string[] Transports { get; set; } = [];

        /// <summary>
        /// Authenticator attestation GUID.
        /// </summary>
        [Column("aaguid")]
        public Guid AaGuid { get; set; }

        /// <summary>
        /// Whether the credential can be backed up by the authenticator provider.
        /// </summary>
        [Column("is_backup_eligible")]
        public bool IsBackupEligible { get; set; }

        /// <summary>
        /// Whether the credential was backed up by the authenticator provider.
        /// </summary>
        [Column("is_backed_up")]
        public bool IsBackedUp { get; set; }

        /// <summary>
        /// Attestation format reported during passkey registration.
        /// </summary>
        [Column("attestation_format")]
        [MaxLength(64)]
        public string? AttestationFormat { get; set; }

        /// <summary>
        /// UTC timestamp when the credential was last used.
        /// </summary>
        [Column("last_used_at")]
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Navigation property for the associated user.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }
}
