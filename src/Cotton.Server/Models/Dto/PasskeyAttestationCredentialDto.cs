// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the passkey attestation credential API payload.
    /// </summary>
    public class PasskeyAttestationCredentialDto
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        public string Id { get; set; } = null!;
        /// <summary>
        /// Gets or sets raw id.
        /// </summary>
        public string RawId { get; set; } = null!;
        /// <summary>
        /// Gets or sets type.
        /// </summary>
        public string Type { get; set; } = null!;
        /// <summary>
        /// Gets or sets the authenticator transports reported by the browser.
        /// </summary>
        public string[] Transports { get; set; } = [];
        /// <summary>
        /// Gets or sets response.
        /// </summary>
        public PasskeyAttestationResponseDto Response { get; set; } = null!;
    }
}
