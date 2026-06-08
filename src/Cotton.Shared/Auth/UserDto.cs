// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using System;
using System.Collections.Generic;

namespace Cotton.Auth
{
    /// <summary>
    /// Represents the current authenticated Cotton user.
    /// </summary>
    public class UserDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets the normalized username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the email address is verified.
        /// </summary>
        public bool IsEmailVerified { get; set; }

        /// <summary>
        /// Gets or sets the numeric server user role value.
        /// </summary>
        public int Role { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether TOTP is enabled.
        /// </summary>
        public bool IsTotpEnabled { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when TOTP was enabled.
        /// </summary>
        public DateTime? TotpEnabledAt { get; set; }

        /// <summary>
        /// Gets or sets the current failed TOTP attempt count.
        /// </summary>
        public int TotpFailedAttempts { get; set; }

        /// <summary>
        /// Gets or sets the optional first name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Gets or sets the optional last name.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Gets or sets the optional birth date.
        /// </summary>
        public DateOnly? BirthDate { get; set; }

        /// <summary>
        /// Gets or sets the encrypted avatar hash token.
        /// </summary>
        public string? AvatarHashEncryptedHex { get; set; }

        /// <summary>
        /// Gets or sets user interface and behavior preferences.
        /// </summary>
        public Dictionary<string, string> Preferences { get; set; } = [];
    }
}
