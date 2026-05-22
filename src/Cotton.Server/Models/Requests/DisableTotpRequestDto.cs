// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the disable totp request payload accepted by the API.
    /// </summary>
    public record DisableTotpRequestDto
    {
        /// <summary>
        /// Gets or sets the password submitted by the client.
        /// </summary>
        public string Password { get; init; } = null!;
    }
}
