// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the reset password request payload accepted by the API.
    /// </summary>
    public class ResetPasswordRequestDto
    {
        /// <summary>
        /// Gets or sets the opaque token submitted by the client.
        /// </summary>
        public string Token { get; set; } = null!;
        /// <summary>
        /// Gets or sets new password.
        /// </summary>
        public string NewPassword { get; set; } = null!;
    }
}
