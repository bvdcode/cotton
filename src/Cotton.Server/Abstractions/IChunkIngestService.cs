// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Abstractions
{
    public interface IChunkIngestService
    {
        Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, CancellationToken ct = default);

        Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, byte[] expectedHash, CancellationToken ct = default);

        Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, CancellationToken ct = default);

        /// <summary>
        /// Records ownership for an already stored cross-user deduplicated chunk.
        /// </summary>
        Task<Chunk> ReuseExistingChunkAsync(Guid userId, byte[] chunkHash, CancellationToken ct = default);
    }
}
