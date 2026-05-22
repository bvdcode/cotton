// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the move node request request payload accepted by the API.
    /// </summary>
    public class MoveNodeRequest
    {
        /// <summary>
        /// Gets or sets parent id.
        /// </summary>
        public Guid ParentId { get; set; }
    }
}
