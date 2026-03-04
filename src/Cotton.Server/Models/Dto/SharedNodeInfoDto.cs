// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class SharedNodeInfoDto
    {
        public string Token { get; set; } = null!;
        public Guid NodeId { get; set; }
        public string Name { get; set; } = null!;
        public DateTime? ExpiresAt { get; set; }
    }
}
