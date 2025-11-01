// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeContentDto : BaseDto<Guid>
    {
        public IEnumerable<NodeDto> Nodes { get; set; } = [];
        public IEnumerable<FileManifestDto> Files { get; set; } = [];
    }
}
