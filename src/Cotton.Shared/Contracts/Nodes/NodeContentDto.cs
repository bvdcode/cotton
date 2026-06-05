// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Common;
using Cotton.Contracts.Files;

namespace Cotton.Contracts.Nodes
{
    /// <summary>
    /// Represents one page of child nodes and files under a Cotton node.
    /// </summary>
    public class NodeContentDto : BaseApiDto
    {
        /// <summary>
        /// Gets or sets the total number of child entries matching the request.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Gets or sets the child nodes returned by the request.
        /// </summary>
        public List<NodeDto> Nodes { get; set; } = [];

        /// <summary>
        /// Gets or sets the child files returned by the request.
        /// </summary>
        public List<NodeFileManifestDto> Files { get; set; } = [];
    }
}
