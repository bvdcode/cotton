// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{

    /// <summary>
    /// Represents the authenticated desktop session exposed to the application shell.
    /// </summary>
    public sealed class AuthSession
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthSession" /> class.
        /// </summary>
        public AuthSession(Guid userId, string username, string? email, bool isTotpEnabled)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            UserId = userId;
            Username = username;
            Email = email;
            IsTotpEnabled = isTotpEnabled;
        }

        /// <summary>
        /// Gets the authenticated user identifier.
        /// </summary>
        public Guid UserId { get; }

        /// <summary>
        /// Gets the authenticated username.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Gets the optional authenticated user email.
        /// </summary>
        public string? Email { get; }

        /// <summary>
        /// Gets a value indicating whether TOTP is enabled for the authenticated user.
        /// </summary>
        public bool IsTotpEnabled { get; }
    }
}
