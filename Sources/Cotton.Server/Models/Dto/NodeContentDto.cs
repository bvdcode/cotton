// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeContentDto : BaseDto<Guid>
    {
        public IEnumerable<NodeDto> Nodes { get; set; } = [];
        public IEnumerable<NodeFileManifestDto> Files { get; set; } = [];
    }
}
