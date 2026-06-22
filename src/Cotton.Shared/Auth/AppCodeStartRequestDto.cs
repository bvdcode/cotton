// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Auth
{
    /// <summary>
    /// Request payload for starting an app-code authorization request.
    /// </summary>
    public class AppCodeStartRequestDto
    {
        /// <summary>
        /// Gets or sets the name of the requesting application.
        /// </summary>
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version of the requesting application.
        /// </summary>
        public string? ApplicationVersion { get; set; }

        /// <summary>
        /// Gets or sets the optional device name shown in session history.
        /// </summary>
        public string? DeviceName { get; set; }
    }
}
