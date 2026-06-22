// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the finish passkey registration request payload accepted by the API.
    /// </summary>
    public class FinishPasskeyRegistrationRequestDto
    {
        /// <summary>
        /// Gets or sets the server-issued passkey ceremony request identifier.
        /// </summary>
        public string RequestId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Gets or sets the WebAuthn credential payload returned by the browser.
        /// </summary>
        public PasskeyAttestationCredentialDto Credential { get; set; } = null!;
    }
}
