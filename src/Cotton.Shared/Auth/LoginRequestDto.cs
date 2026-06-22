// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;

namespace Cotton.Auth
{
    /// <summary>
    /// Represents the username and password login request accepted by Cotton Cloud.
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>
        /// Gets or sets the username or email address used for authentication.
        /// </summary>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password used for authentication.
        /// </summary>
        [Required]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional first name used when public instances auto-create a user.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Gets or sets the optional last name used when public instances auto-create a user.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Gets or sets the optional two-factor authentication code.
        /// </summary>
        public string? TwoFactorCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the device should be trusted for future login attempts.
        /// </summary>
        public bool TrustDevice { get; set; }
    }
}
