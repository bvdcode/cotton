// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Files
{
    /// <summary>
    /// Represents a rename-file request.
    /// </summary>
    public class RenameFileRequestDto
    {
        /// <summary>
        /// Gets or sets the new display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
