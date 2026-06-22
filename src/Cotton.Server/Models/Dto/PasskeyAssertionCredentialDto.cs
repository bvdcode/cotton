// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the passkey assertion credential API payload.
    /// </summary>
    public class PasskeyAssertionCredentialDto
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
        /// Gets or sets response.
        /// </summary>
        public PasskeyAssertionResponseDto Response { get; set; } = null!;
    }
}
