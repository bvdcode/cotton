// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Authenticator response from a WebAuthn authentication (assertion) ceremony.
    /// </summary>
    public class PasskeyAssertionResponseDto
    {
        /// <summary>
        /// Base64url-encoded authenticator data produced during the assertion.
        /// </summary>
        public string AuthenticatorData { get; set; } = null!;

        /// <summary>
        /// Base64url-encoded client data JSON that the authenticator signed over.
        /// </summary>
        public string ClientDataJson { get; set; } = null!;

        /// <summary>
        /// Base64url-encoded signature over the authenticator data and client data hash.
        /// </summary>
        public string Signature { get; set; } = null!;

        /// <summary>
        /// Base64url-encoded user handle returned by the authenticator, if provided.
        /// </summary>
        public string? UserHandle { get; set; }
    }
}
