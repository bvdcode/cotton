// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class SharedNodeInfoDto
    {
        /// <summary>
        /// Gets or sets the opaque token submitted by the client.
        /// </summary>
        public string Token { get; set; } = null!;

        public Guid NodeId { get; set; }

        public string Name { get; set; } = null!;

        public DateTime? ExpiresAt { get; set; }
    }
}
