// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Collections.Generic;

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

        /// <summary>Metadata changes stored with the name; null values remove keys.</summary>
        public Dictionary<string, string?>? Metadata { get; set; }
    }
}
