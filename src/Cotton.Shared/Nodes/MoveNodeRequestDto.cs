// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Nodes
{
    /// <summary>
    /// Represents a move-node request.
    /// </summary>
    public class MoveNodeRequestDto
    {
        /// <summary>
        /// Gets or sets the target parent node identifier.
        /// </summary>
        public Guid ParentId { get; set; }

        /// <summary>
        /// Gets or sets the destination name. When omitted, the current name is preserved.
        /// </summary>
        public string? Name { get; set; }
    }
}
