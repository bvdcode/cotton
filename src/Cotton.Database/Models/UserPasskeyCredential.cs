// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("user_passkey_credentials")]
    [Index(nameof(CredentialId), IsUnique = true)]
    [Index(nameof(UserId))]
    public class UserPasskeyCredential : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("credential_id")]
        public byte[] CredentialId { get; set; } = [];

        [Column("public_key")]
        public byte[] PublicKey { get; set; } = [];

        [Column("user_handle")]
        public byte[] UserHandle { get; set; } = [];

        [Column("signature_counter")]
        public long SignatureCounter { get; set; }

        [Column("name")]
        [MaxLength(120)]
        public string Name { get; set; } = null!;

        [Column("transports")]
        public string[] Transports { get; set; } = [];

        [Column("aaguid")]
        public Guid AaGuid { get; set; }

        [Column("is_backup_eligible")]
        public bool IsBackupEligible { get; set; }

        [Column("is_backed_up")]
        public bool IsBackedUp { get; set; }

        [Column("attestation_format")]
        [MaxLength(64)]
        public string? AttestationFormat { get; set; }

        [Column("last_used_at")]
        public DateTime? LastUsedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }
}
