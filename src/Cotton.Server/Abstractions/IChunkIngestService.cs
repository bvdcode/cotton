// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Defines the chunk ingest service contract used by the server runtime.
    /// </summary>
    public interface IChunkIngestService
    {
        /// <summary>
        /// Stores a chunk from an already-buffered payload and derives its SHA-256 storage key.
        /// </summary>
        Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, CancellationToken ct = default);

        /// <summary>
        /// Stores a chunk from a stream after verifying it against a caller-provided SHA-256 hash.
        /// </summary>
        Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, byte[] expectedHash, CancellationToken ct = default);

        /// <summary>
        /// Stores a chunk from a stream when no caller-provided hash is available.
        /// </summary>
        Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, CancellationToken ct = default);
    }
}
