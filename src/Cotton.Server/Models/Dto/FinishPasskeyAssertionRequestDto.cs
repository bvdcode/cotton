// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the finish passkey assertion request payload accepted by the API.
    /// </summary>
    public class FinishPasskeyAssertionRequestDto
    {
        /// <summary>
        /// Gets or sets the server-issued passkey ceremony request identifier.
        /// </summary>
        public string RequestId { get; set; } = null!;

        /// <summary>
        /// Whether to remember this device and issue a longer-lived session.
        /// </summary>
        public bool TrustDevice { get; set; }

        /// <summary>
        /// Gets or sets the WebAuthn credential payload returned by the browser.
        /// </summary>
        public PasskeyAssertionCredentialDto Credential { get; set; } = null!;
    }
}
