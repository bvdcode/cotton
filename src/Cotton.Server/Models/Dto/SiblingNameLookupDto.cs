// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Server.Models.Dto
{
    public class SiblingNameLookupDto
    {
        public List<NodeDto> Nodes { get; set; } = [];

        public List<FileNameMatchDto> Files { get; set; } = [];

        public List<string> TakenNameKeys { get; set; } = [];
    }
}
