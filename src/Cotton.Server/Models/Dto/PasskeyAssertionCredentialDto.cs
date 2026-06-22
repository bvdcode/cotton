// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Public-key credential returned by the browser after a WebAuthn authentication ceremony.
    /// </summary>
    public class PasskeyAssertionCredentialDto
    {
        /// <summary>
        /// Credential id (base64url).
        /// </summary>
        public string Id { get; set; } = null!;

        /// <summary>
        /// Raw credential id (base64url).
        /// </summary>
        public string RawId { get; set; } = null!;

        /// <summary>
        /// Credential type; always "public-key".
        /// </summary>
        public string Type { get; set; } = null!;

        /// <summary>
        /// Authenticator assertion response carrying the signature.
        /// </summary>
        public PasskeyAssertionResponseDto Response { get; set; } = null!;
    }
}
