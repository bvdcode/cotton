// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Describes a username/password desktop sign-in attempt.
    /// </summary>
    public class PasswordSignInRequest
    {
        /// <summary>
        /// Gets or sets the username or email address used for authentication.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password used for authentication.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional TOTP code.
        /// </summary>
        public string? TwoFactorCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the device should be trusted.
        /// </summary>
        public bool TrustDevice { get; set; }

        /// <summary>
        /// Gets or sets the optional first name used by public instances that auto-create users.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Gets or sets the optional last name used by public instances that auto-create users.
        /// </summary>
        public string? LastName { get; set; }
    }
}
