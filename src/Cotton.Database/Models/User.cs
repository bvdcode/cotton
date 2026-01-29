// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("users")]
    [Index(nameof(Username), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        [Column("username", TypeName = "citext")]
        public string Username { get; set; } = null!;

        [Column("password_phc")]
        public string PasswordPhc { get; set; } = null!;

        [Column("webdav_token_phc")]
        public string WebDavTokenPhc { get; set; } = null!;

        [Column("role")]
        public UserRole Role { get; set; }

        [Column("is_totp_enabled")]
        public bool IsTotpEnabled { get; set; }

        [Column("totp_secret_encrypted")]
        public byte[]? TotpSecretEncrypted { get; set; }

        [Column("totp_enabled_at")]
        public DateTime? TotpEnabledAt { get; set; }

        [Column("totp_failed_attempts")]
        public int TotpFailedAttempts { get; set; }
    }
}
