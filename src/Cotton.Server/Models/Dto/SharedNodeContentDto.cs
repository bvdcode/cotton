// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class SharedNodeContentDto : BaseDto<Guid>
    {
        public IEnumerable<NodeDto> Nodes { get; set; } = [];

        public IEnumerable<SharedNodeFileDto> Files { get; set; } = [];
    }
}
