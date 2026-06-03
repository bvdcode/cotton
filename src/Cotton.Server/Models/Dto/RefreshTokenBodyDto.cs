// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents a request that carries a refresh token in the HTTP body.
    /// </summary>
    public class RefreshTokenBodyDto
    {
        /// <summary>
        /// Gets or sets refresh token.
        /// </summary>
        public string? RefreshToken { get; set; }
    }
}
