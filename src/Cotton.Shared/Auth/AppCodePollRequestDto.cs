// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Auth
{
    /// <summary>
    /// Request payload for polling an app-code authorization request.
    /// </summary>
    public class AppCodePollRequestDto
    {
        /// <summary>
        /// Gets or sets the secret polling token returned from the start endpoint.
        /// </summary>
        public string PollToken { get; set; } = string.Empty;
    }
}
