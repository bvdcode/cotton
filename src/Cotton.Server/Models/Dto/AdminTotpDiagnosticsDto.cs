// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the admin totp diagnostics API payload.
    /// </summary>
    public class AdminTotpDiagnosticsDto
    {
        /// <summary>
        /// Gets or sets admin count.
        /// </summary>
        public int AdminCount { get; init; }
        /// <summary>
        /// Gets or sets admins with totp.
        /// </summary>
        public int AdminsWithTotp { get; init; }
        /// <summary>
        /// Gets or sets admins without totp.
        /// </summary>
        public int AdminsWithoutTotp { get; init; }
    }
}
