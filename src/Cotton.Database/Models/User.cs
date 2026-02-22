// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("users")]
    [Index(nameof(Username), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        [Column("username", TypeName = "citext")]
        [MinLength(2)]
        [MaxLength(32)]
        [RegularExpression("^[a-z][a-z0-9]{1,31}$")]
        public string Username { get; set; } = null!;

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("birth_date")]
        public DateOnly? BirthDate { get; set; }

        [Column("password_phc")]
        public string PasswordPhc { get; set; } = null!;

        [Column("webdav_token_phc")]
        public string WebDavTokenPhc { get; set; } = null!;

        [Column("email", TypeName = "citext")]
        public string? Email { get; set; }

        [Column("is_email_verified")]
        public bool IsEmailVerified { get; set; }

        [Column("email_verification_token")]
        public string? EmailVerificationToken { get; set; }

        [Column("email_verification_token_sent_at")]
        public DateTime? EmailVerificationTokenSentAt { get; set; }

        [Column("password_reset_token")]
        public string? PasswordResetToken { get; set; }

        [Column("password_reset_token_sent_at")]
        public DateTime? PasswordResetTokenSentAt { get; set; }

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

        [Column("preferences")]
        public Dictionary<string, string> Preferences { get; set; } = [];

        public virtual ICollection<ChunkOwnership> ChunkOwnerships { get; set; } = [];
        public virtual ICollection<DownloadToken> DownloadTokens { get; set; } = [];
        public virtual ICollection<Notification> Notifications { get; set; } = [];
        public virtual ICollection<NodeFile> NodeFiles { get; set; } = [];
    }
}
