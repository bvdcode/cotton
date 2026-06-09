// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote
{
    internal record UploadedChunks(List<string> ChunkHashes, string ContentHash);
}
