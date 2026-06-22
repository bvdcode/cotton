// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the passkey assertion response API payload.
    /// </summary>
    public class PasskeyAssertionResponseDto
    {
        /// <summary>
        /// Gets or sets authenticator data.
        /// </summary>
        public string AuthenticatorData { get; set; } = null!;
        /// <summary>
        /// Gets or sets client data json.
        /// </summary>
        public string ClientDataJson { get; set; } = null!;
        /// <summary>
        /// Gets or sets signature.
        /// </summary>
        public string Signature { get; set; } = null!;
        /// <summary>
        /// Gets or sets user handle.
        /// </summary>
        public string? UserHandle { get; set; }
    }
}
