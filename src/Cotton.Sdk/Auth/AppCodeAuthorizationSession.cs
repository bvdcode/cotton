// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Auth
{
    /// <summary>
    /// Represents a started browser app-code authorization session.
    /// </summary>
    public class AppCodeAuthorizationSession
    {
        /// <summary>
        /// Gets or sets the browser approval request id.
        /// </summary>
        public Guid ApprovalId { get; set; }

        /// <summary>
        /// Gets or sets the absolute browser approval URI.
        /// </summary>
        public Uri ApprovalUri { get; set; } = new("about:blank");

        /// <summary>
        /// Gets or sets the secret polling token returned by the server.
        /// </summary>
        public string PollToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the session expires.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the server-suggested polling interval.
        /// </summary>
        public TimeSpan PollInterval { get; set; }
    }
}
