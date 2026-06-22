// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the begin passkey registration request payload accepted by the API.
    /// </summary>
    public class BeginPasskeyRegistrationRequestDto
    {
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string? Name { get; set; }
    }
}
