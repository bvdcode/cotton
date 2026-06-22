// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the passkey assertion options response API payload.
    /// </summary>
    public class PasskeyAssertionOptionsResponseDto
    {
        /// <summary>
        /// Gets or sets the server-issued passkey ceremony request identifier.
        /// </summary>
        public string RequestId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the WebAuthn options returned to the browser.
        /// </summary>
        public AssertionOptions Options { get; set; } = null!;
    }
}
