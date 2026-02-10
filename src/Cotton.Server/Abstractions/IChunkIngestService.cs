// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>


// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Abstractions;

public interface IChunkIngestService
{
    Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, CancellationToken ct = default);
    Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, CancellationToken ct = default);
}
