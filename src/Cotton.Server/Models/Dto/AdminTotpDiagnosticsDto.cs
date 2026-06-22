// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Administrator TOTP (two-factor authentication) coverage diagnostics.
    /// </summary>
    public class AdminTotpDiagnosticsDto
    {
        /// <summary>
        /// Total number of administrator accounts.
        /// </summary>
        public int AdminCount { get; init; }

        /// <summary>
        /// Number of administrators with TOTP enabled.
        /// </summary>
        public int AdminsWithTotp { get; init; }

        /// <summary>
        /// Number of administrators without TOTP enabled.
        /// </summary>
        public int AdminsWithoutTotp { get; init; }
    }
}
