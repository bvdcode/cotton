// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// A single security warning raised while collecting diagnostics.
    /// </summary>
    public class SecurityDiagnosticWarningDto
    {
        /// <summary>
        /// Stable code identifying the warning.
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// Warning severity (e.g. info, warning, critical).
        /// </summary>
        public string Severity { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable description of the warning.
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}
