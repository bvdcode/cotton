// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk
{
    /// <summary>
    /// Configures a Cotton SDK client instance.
    /// </summary>
    public class CottonSdkOptions
    {
        /// <summary>
        /// Gets or sets the Cotton server base address.
        /// </summary>
        public Uri BaseAddress { get; set; } = new("http://localhost:5182");

        /// <summary>
        /// Gets or sets a value indicating whether the SDK should refresh tokens after an unauthorized response.
        /// </summary>
        public bool RefreshOnUnauthorized { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional user agent sent with SDK HTTP requests.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Gets or sets an optional user-visible device name sent when the server issues sessions.
        /// </summary>
        public string? DeviceName { get; set; }
    }
}
