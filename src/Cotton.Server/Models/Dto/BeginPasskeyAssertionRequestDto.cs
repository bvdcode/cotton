// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the begin passkey assertion request payload accepted by the API.
    /// </summary>
    public class BeginPasskeyAssertionRequestDto
    {
        /// <summary>
        /// Gets or sets the normalized username.
        /// </summary>
        public string? Username { get; set; }
    }
}
