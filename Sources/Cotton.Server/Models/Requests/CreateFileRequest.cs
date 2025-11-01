// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Cotton.Server.Models.Requests
{
    public class CreateFileRequest
    {
        public Guid NodeId { get; set; }
        public string[] ChunkHashes { get; set; } = [];
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string Sha256 { get; set; } = null!;
        public Guid? OriginalNodeFileId { get; set; }
    }
}
