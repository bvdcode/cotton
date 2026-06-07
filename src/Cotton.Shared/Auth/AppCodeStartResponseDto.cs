// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Auth
{
    /// <summary>
    /// Response payload returned after starting an app-code authorization request.
    /// </summary>
    public class AppCodeStartResponseDto
    {
        /// <summary>
        /// Gets or sets the browser approval request id.
        /// </summary>
        public Guid ApprovalId { get; set; }

        /// <summary>
        /// Gets or sets the browser path where the user can approve the request.
        /// </summary>
        public string ApprovalUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the secret token used by the application to poll for completion.
        /// </summary>
        public string PollToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the request expires.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the suggested polling interval in seconds.
        /// </summary>
        public int PollIntervalSeconds { get; set; }
    }
}
