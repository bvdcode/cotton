// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Handlers.Files
{
    public record ResolvedOwnedFileContent(
        NodeFile NodeFile,
        FileManifestChunk? Chunk,
        int ChunkCount);
}
