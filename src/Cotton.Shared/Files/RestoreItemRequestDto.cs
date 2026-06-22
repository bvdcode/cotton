// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Files
{
    /// <summary>
    /// Represents a restore request for a trashed node or file.
    /// </summary>
    public class RestoreItemRequestDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether missing parent folders should be recreated.
        /// </summary>
        public bool CreateMissingParents { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether existing target entries can be overwritten.
        /// </summary>
        public bool Overwrite { get; set; }
    }
}
