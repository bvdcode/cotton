// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Request used to create an OpenID Connect authorization URL.
    /// </summary>
    public class OidcAuthorizationRequestDto
    {
        /// <summary>
        /// Application return URL after the OIDC callback completes.
        /// </summary>
        public string? ReturnUrl { get; set; }

        /// <summary>
        /// Whether the issued session should be trusted on this device.
        /// </summary>
        public bool TrustDevice { get; set; }
    }
}
