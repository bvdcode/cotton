// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Auth
{
    /// <summary>
    /// Polling error payload for app-code authorization requests.
    /// </summary>
    public class AppCodePollErrorDto
    {
        /// <summary>
        /// Gets or sets the machine-readable polling error.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional retry interval in seconds.
        /// </summary>
        public int? RetryAfterSeconds { get; set; }
    }
}
