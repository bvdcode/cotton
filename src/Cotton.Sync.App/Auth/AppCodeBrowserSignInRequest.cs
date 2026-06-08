// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Represents a browser app-code sign-in request for native clients.
    /// </summary>
    public class AppCodeBrowserSignInRequest
    {
        /// <summary>
        /// Gets or sets the requesting application name shown to the user.
        /// </summary>
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the requesting application version shown to the user.
        /// </summary>
        public string? ApplicationVersion { get; set; }

        /// <summary>
        /// Gets or sets the user-visible device name shown in session history.
        /// </summary>
        public string? DeviceName { get; set; }
    }
}
