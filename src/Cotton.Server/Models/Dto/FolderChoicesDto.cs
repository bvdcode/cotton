// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Server.Models.Dto
{
    public class FolderChoicesDto
    {
        public NodeDto Current { get; set; } = null!;
        public IReadOnlyList<NodeDto> Folders { get; set; } = [];
        public bool HasMore { get; set; }
    }
}
