// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Nodes
{
    /// <summary>
    /// Represents a rename-node request.
    /// </summary>
    public class RenameNodeRequestDto
    {
        /// <summary>
        /// Gets or sets the new display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
