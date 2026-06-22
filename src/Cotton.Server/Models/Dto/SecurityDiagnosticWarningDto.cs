// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the security diagnostic warning API payload.
    /// </summary>
    public class SecurityDiagnosticWarningDto
    {
        /// <summary>
        /// Gets or sets code.
        /// </summary>
        public string Code { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets severity.
        /// </summary>
        public string Severity { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets message.
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}
