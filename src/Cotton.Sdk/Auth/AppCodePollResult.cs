// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;

namespace Cotton.Sdk.Auth
{
    /// <summary>
    /// Represents the result of polling a browser app-code authorization session.
    /// </summary>
    public class AppCodePollResult
    {
        /// <summary>
        /// Gets or sets the polling outcome.
        /// </summary>
        public AppCodePollStatus Status { get; set; }

        /// <summary>
        /// Gets or sets issued tokens when the session is approved.
        /// </summary>
        public TokenPairDto? Tokens { get; set; }

        /// <summary>
        /// Gets or sets the machine-readable server error for non-approved outcomes.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the server-suggested retry delay.
        /// </summary>
        public TimeSpan? RetryAfter { get; set; }
    }
}
