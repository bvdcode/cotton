// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Auth
{
    /// <summary>
    /// Browser-facing details for an app-code authorization request.
    /// </summary>
    public class AppCodeDetailsDto
    {
        /// <summary>
        /// Gets or sets the authorization request id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the requesting application.
        /// </summary>
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version of the requesting application.
        /// </summary>
        public string ApplicationVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional device name supplied by the requesting application.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the origin address of the request.
        /// </summary>
        public string Origin { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the request was created.
        /// </summary>
        public DateTime RequestedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the request expires.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the current request status.
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }
}
