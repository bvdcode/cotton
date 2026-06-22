// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Public-key credential returned by the browser after a WebAuthn registration ceremony.
    /// </summary>
    public class PasskeyAttestationCredentialDto
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
        /// Authenticator transports reported by the browser.
        /// </summary>
        public string[] Transports { get; set; } = [];

        /// <summary>
        /// Authenticator attestation response carrying the credential public key.
        /// </summary>
        public PasskeyAttestationResponseDto Response { get; set; } = null!;
    }
}
