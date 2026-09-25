// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Files
{
    /// <summary>
    /// Describes a downloaded chunk and the version of its containing file.
    /// </summary>
    public record CottonContentChunkDownloadResult(int ChunkCount, string ETag);
}
