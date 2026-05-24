// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the node content API payload.
    /// </summary>
    public class NodeContentDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets total count.
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// Gets or sets nodes.
        /// </summary>
        public IEnumerable<NodeDto> Nodes { get; set; } = [];
        /// <summary>
        /// Gets or sets files.
        /// </summary>
        public IEnumerable<NodeFileManifestDto> Files { get; set; } = [];
    }
}
