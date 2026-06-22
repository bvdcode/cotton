// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Authenticator response from a WebAuthn registration (attestation) ceremony.
    /// </summary>
    public class PasskeyAttestationResponseDto
    {
        /// <summary>
        /// Base64url-encoded CBOR attestation object containing the new credential's public key.
        /// </summary>
        public string AttestationObject { get; set; } = null!;

        /// <summary>
        /// Base64url-encoded client data JSON that the authenticator signed over.
        /// </summary>
        public string ClientDataJson { get; set; } = null!;
    }
}
