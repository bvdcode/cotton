// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class CopyItemRequestDto
    {
        public Guid ParentId { get; set; }
        public string? Name { get; set; }
        public bool Overwrite { get; set; }
    }
}
