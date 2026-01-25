// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class SearchResultDto
    {
        public IEnumerable<NodeDto> Nodes { get; set; } = [];
        public IEnumerable<FileManifestDto> Files { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
