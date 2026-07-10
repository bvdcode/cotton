// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the rename passkey request payload accepted by the API.
    /// </summary>
    public class RenamePasskeyRequestDto
    {
        /// <summary>
        /// Gets or sets the optional user-authored label. A blank value removes the label.
        /// </summary>
        public string? Label { get; set; }
    }
}
