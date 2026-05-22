// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the forgot password request payload accepted by the API.
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        /// <summary>
        /// Gets or sets username or email.
        /// </summary>
        public string UsernameOrEmail { get; set; } = null!;
    }
}
