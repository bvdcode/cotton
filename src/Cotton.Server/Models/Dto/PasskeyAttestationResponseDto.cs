// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the passkey attestation response API payload.
    /// </summary>
    public class PasskeyAttestationResponseDto
    {
        /// <summary>
        /// Gets or sets attestation object.
        /// </summary>
        public string AttestationObject { get; set; } = null!;
        /// <summary>
        /// Gets or sets client data json.
        /// </summary>
        public string ClientDataJson { get; set; } = null!;
    }
}
