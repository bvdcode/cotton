// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;

namespace Cotton.Server.Models.Dto
{
    public class SearchResultDto
    {
        public IEnumerable<NodeDto> Nodes { get; set; } = [];

        public IEnumerable<NodeFileManifestDto> Files { get; set; } = [];

        public IDictionary<Guid, string> NodePaths { get; set; } = new Dictionary<Guid, string>();

        public IDictionary<Guid, string> FilePaths { get; set; } = new Dictionary<Guid, string>();
    }
}
