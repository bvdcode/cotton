// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeContentDto : BaseDto<Guid>
    {
        public int TotalCount { get; set; }
        public IEnumerable<NodeDto> Nodes { get; set; } = [];
        public IEnumerable<FileManifestDto> Files { get; set; } = [];
    }
}
