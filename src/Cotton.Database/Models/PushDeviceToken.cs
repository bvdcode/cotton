// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Represents one mobile device token registered for server push notifications.
    /// </summary>
    [Index(nameof(UserId), nameof(Provider), nameof(TokenHash), IsUnique = true)]
    [Index(nameof(UserId), nameof(SessionId), nameof(Provider), nameof(Platform))]
    [Table("push_device_tokens")]
    public class PushDeviceToken : BaseEntity<Guid>
    {
        /// <summary>Maximum accepted push token length.</summary>
        public const int TokenMaxLength = 4096;
        /// <summary>SHA-256 token hash hex length.</summary>
        public const int TokenHashLength = 64;
        /// <summary>Maximum stored auth session identifier length.</summary>
        public const int SessionIdMaxLength = 128;
        /// <summary>Maximum stored device name length.</summary>
        public const int DeviceNameMaxLength = 128;
        /// <summary>Maximum stored app version length.</summary>
        public const int AppVersionMaxLength = 64;

        /// <summary>Identifier of the user who owns the token.</summary>
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>Server-side push provider for the token.</summary>
        [Column("provider")]
        public PushDeviceTokenProvider Provider { get; set; }

        /// <summary>Mobile platform for the token.</summary>
        [Column("platform")]
        public PushDeviceTokenPlatform Platform { get; set; }

        /// <summary>Push provider registration token.</summary>
        [Column("token")]
        [MaxLength(TokenMaxLength)]
        public string Token { get; set; } = null!;

        /// <summary>SHA-256 hash of the push provider registration token.</summary>
        [Column("token_hash")]
        [MaxLength(TokenHashLength)]
        public string TokenHash { get; set; } = null!;

        /// <summary>Auth session identifier that registered the token.</summary>
        [Column("session_id")]
        [MaxLength(SessionIdMaxLength)]
        public string SessionId { get; set; } = null!;

        /// <summary>Optional client-reported device name.</summary>
        [Column("device_name")]
        [MaxLength(DeviceNameMaxLength)]
        public string? DeviceName { get; set; }

        /// <summary>Optional client-reported application version.</summary>
        [Column("app_version")]
        [MaxLength(AppVersionMaxLength)]
        public string? AppVersion { get; set; }

        /// <summary>UTC timestamp of the latest registration or refresh for this token.</summary>
        [Column("last_registered_at")]
        public DateTime LastRegisteredAt { get; set; }

        /// <summary>UTC timestamp when the token was revoked.</summary>
        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        /// <summary>Navigation property for the owning user.</summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User User { get; set; } = null!;
    }
}
