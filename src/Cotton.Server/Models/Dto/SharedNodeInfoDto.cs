// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the shared node info API payload.
    /// </summary>
    public class SharedNodeInfoDto
    {
        /// <summary>
        /// Gets or sets the opaque token submitted by the client.
        /// </summary>
        public string Token { get; set; } = null!;

        /// <summary>
        /// Gets or sets node id.
        /// </summary>
        public Guid NodeId { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Gets or sets expires at.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
