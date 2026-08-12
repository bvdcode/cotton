// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class ResetPasswordRequestDto
    {
        /// <summary>
        /// Gets or sets the opaque token submitted by the client.
        /// </summary>
        public string Token { get; set; } = null!;

        public string NewPassword { get; set; } = null!;
    }
}
