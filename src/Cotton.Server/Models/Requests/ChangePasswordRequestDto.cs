// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the change password request payload accepted by the API.
    /// </summary>
    public class ChangePasswordRequestDto
    {
        /// <summary>
        /// Gets or sets old password.
        /// </summary>
        public string OldPassword { get; set; } = null!;
        /// <summary>
        /// Gets or sets new password.
        /// </summary>
        public string NewPassword { get; set; } = null!;
    }
}
