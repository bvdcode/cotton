// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the user API payload.
    /// </summary>
    public class UserDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets the normalized username.
        /// </summary>
        public string Username { get; set; } = null!;
        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        public string? Email { get; set; }
        /// <summary>
        /// Indicates whether email verified.
        /// </summary>
        public bool IsEmailVerified { get; set; }
        /// <summary>
        /// Gets or sets the user role.
        /// </summary>
        public UserRole Role { get; set; }
        /// <summary>
        /// Indicates whether totp enabled.
        /// </summary>
        public bool IsTotpEnabled { get; set; }
        /// <summary>
        /// Gets or sets totp enabled at.
        /// </summary>
        public DateTime? TotpEnabledAt { get; set; }
        /// <summary>
        /// Gets or sets totp failed attempts.
        /// </summary>
        public int TotpFailedAttempts { get; set; }
        /// <summary>
        /// Gets or sets first name.
        /// </summary>
        public string? FirstName { get; set; }
        /// <summary>
        /// Gets or sets last name.
        /// </summary>
        public string? LastName { get; set; }
        /// <summary>
        /// Gets or sets birth date.
        /// </summary>
        public DateOnly? BirthDate { get; set; }
        /// <summary>
        /// Gets or sets avatar hash encrypted hex.
        /// </summary>
        public string? AvatarHashEncryptedHex { get; set; }
        /// <summary>
        /// Gets or sets user interface and behavior preferences.
        /// </summary>
        public Dictionary<string, string> Preferences { get; set; } = [];
    }
}
