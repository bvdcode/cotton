// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;

namespace Cotton.Files
{
    /// <summary>
    /// Represents a move-file request.
    /// </summary>
    public class MoveFileRequestDto
    {
        /// <summary>
        /// Gets or sets the target parent node identifier.
        /// </summary>
        public Guid ParentId { get; set; }

        /// <summary>
        /// Gets or sets the destination name. When omitted, the current name is preserved.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets whether an existing file with the destination name is moved to trash.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Gets or sets metadata changes saved together with the move.
        /// </summary>
        public Dictionary<string, string?>? Metadata { get; set; }
    }
}
