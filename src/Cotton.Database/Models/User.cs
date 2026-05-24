// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Represents a Cotton account and its authentication state.</summary>
    [Table("users")]
    [Index(nameof(Username), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        /// <summary>Prefix used by public preview tokens that are scoped to a user avatar row.</summary>
        public const char AvatarPreviewTokenPrefix = 'u';

        /// <summary>Normalized unique username used for login.</summary>
        [Column("username", TypeName = "citext")]
        [MinLength(2)]
        [MaxLength(32)]
        [RegularExpression("^[a-z][a-z0-9]{1,31}$")]
        public string Username { get; set; } = null!;

        /// <summary>Optional user first name.</summary>
        [Column("first_name")]
        public string? FirstName { get; set; }

        /// <summary>Optional user last name.</summary>
        [Column("last_name")]
        public string? LastName { get; set; }

        /// <summary>Optional user birth date.</summary>
        [Column("birth_date")]
        public DateOnly? BirthDate { get; set; }

        /// <summary>PHC-formatted password hash.</summary>
        [Column("password_phc")]
        public string PasswordPhc { get; set; } = null!;

        /// <summary>PHC-formatted WebDAV token hash.</summary>
        [Column("webdav_token_phc")]
        public string WebDavTokenPhc { get; set; } = null!;

        /// <summary>Optional user email address.</summary>
        [Column("email", TypeName = "citext")]
        public string? Email { get; set; }

        /// <summary>Whether the user email address has been verified.</summary>
        [Column("is_email_verified")]
        public bool IsEmailVerified { get; set; }

        /// <summary>Email verification token pending confirmation.</summary>
        [Column("email_verification_token")]
        public string? EmailVerificationToken { get; set; }

        /// <summary>UTC timestamp when the current email verification token was sent.</summary>
        [Column("email_verification_token_sent_at")]
        public DateTime? EmailVerificationTokenSentAt { get; set; }

        /// <summary>Password reset token pending confirmation.</summary>
        [Column("password_reset_token")]
        public string? PasswordResetToken { get; set; }

        /// <summary>UTC timestamp when the current password reset token was sent.</summary>
        [Column("password_reset_token_sent_at")]
        public DateTime? PasswordResetTokenSentAt { get; set; }

        /// <summary>Application role assigned to the user.</summary>
        [Column("role")]
        public UserRole Role { get; set; }

        /// <summary>Whether TOTP two-factor authentication is enabled.</summary>
        [Column("is_totp_enabled")]
        public bool IsTotpEnabled { get; set; }

        /// <summary>Encrypted TOTP secret.</summary>
        [Column("totp_secret_encrypted")]
        public byte[]? TotpSecretEncrypted { get; set; }

        /// <summary>UTC timestamp when TOTP was enabled.</summary>
        [Column("totp_enabled_at")]
        public DateTime? TotpEnabledAt { get; set; }

        /// <summary>Current count of consecutive failed TOTP attempts.</summary>
        [Column("totp_failed_attempts")]
        public int TotpFailedAttempts { get; set; }

        /// <summary>Encrypted storage hash for the user avatar.</summary>
        [Column("avatar_hash_encrypted")]
        public byte[]? AvatarHashEncrypted { get; set; }

        /// <summary>Plain storage hash for the user avatar when available.</summary>
        [Column("avatar_hash")]
        public byte[]? AvatarHash { get; set; }

        /// <summary>Returns the row-scoped encrypted avatar token as lowercase text.</summary>
        public string? GetAvatarHashEncryptedHex()
        {
            if (AvatarHashEncrypted is null)
            {
                return null;
            }
            return string.Concat(
                AvatarPreviewTokenPrefix,
                Id.ToString("N"),
                Convert.ToHexStringLower(AvatarHashEncrypted));
        }

        /// <summary>User preference key-value data.</summary>
        [Column("preferences")]
        public Dictionary<string, string> Preferences { get; set; } = [];

        /// <summary>Chunk ownership rows used for proof-of-ownership checks.</summary>
        public virtual ICollection<ChunkOwnership> ChunkOwnerships { get; set; } = [];
        /// <summary>Temporary direct-download token rows.</summary>
        public virtual ICollection<DownloadToken> DownloadTokens { get; set; } = [];
        /// <summary>User notification rows.</summary>
        public virtual ICollection<Notification> Notifications { get; set; } = [];
        /// <summary>Visible file entries stored by the server.</summary>
        public virtual ICollection<NodeFile> NodeFiles { get; set; } = [];
        /// <summary>Passkey credentials registered by the user.</summary>
        public virtual ICollection<UserPasskeyCredential> PasskeyCredentials { get; set; } = [];
    }
}
