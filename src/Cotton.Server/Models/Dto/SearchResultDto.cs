// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the search result API payload.
    /// </summary>
    public class SearchResultDto
    {
        /// <summary>
        /// Gets or sets nodes.
        /// </summary>
        public IEnumerable<NodeDto> Nodes { get; set; } = [];
        /// <summary>
        /// Gets or sets files.
        /// </summary>
        public IEnumerable<NodeFileManifestDto> Files { get; set; } = [];

        /// <summary>
        /// Maps node id to its resolved absolute path (within the layout).
        /// </summary>
        public IDictionary<Guid, string> NodePaths { get; set; } = new Dictionary<Guid, string>();

        /// <summary>
        /// Maps node file id to its resolved absolute path (within the layout).
        /// Includes filename as the last segment.
        /// </summary>
        public IDictionary<Guid, string> FilePaths { get; set; } = new Dictionary<Guid, string>();
    }
}
