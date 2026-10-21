// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;

namespace Cotton.Nodes
{
    /// <summary>
    /// Represents a create-node request.
    /// </summary>
    public class CreateNodeRequestDto
    {
        /// <summary>
        /// Gets or sets the parent node identifier.
        /// </summary>
        public Guid ParentId { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Metadata stored atomically with the new folder.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
