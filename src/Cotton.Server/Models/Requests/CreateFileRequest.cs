// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class CreateFileRequest
    {
        public Guid NodeId { get; set; }
        public string[] ChunkHashes { get; set; } = [];
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string Hash { get; set; } = null!;
        public Guid? OriginalNodeFileId { get; set; }
        public bool Validate { get; set; }
    }
}
