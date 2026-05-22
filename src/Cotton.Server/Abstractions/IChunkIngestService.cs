// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Abstractions;

/// <summary>
/// Defines the chunk ingest service contract used by the server runtime.
/// </summary>
public interface IChunkIngestService
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, CancellationToken ct = default);
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, CancellationToken ct = default);
}
