// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the confirm totp request payload accepted by the API.
    /// </summary>
    public record ConfirmTotpRequestDto
    {
        /// <summary>
        /// Gets or sets two factor code.
        /// </summary>
        public string TwoFactorCode { get; init; } = null!;
    }
}
