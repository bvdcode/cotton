// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System;

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
    }
}
