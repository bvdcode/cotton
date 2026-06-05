// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Auth
{
    /// <summary>
    /// Represents an issued access-token and refresh-token pair.
    /// </summary>
    public class TokenPairDto
    {
        /// <summary>
        /// Gets or sets the bearer access token.
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the refresh token used to issue a new access token.
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;
    }
}
