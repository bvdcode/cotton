// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Jobs;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Topology.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Cotton.Server.Services;

public class ChunkIngestService(
    CottonDbContext _dbContext,
    ILayoutService _layouts,
    IStoragePipeline _storage,
    ILogger<ChunkIngestService> _logger)
    : IChunkIngestService
{
    public async Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, CancellationToken ct = default)
    {
        byte[] chunkHash = SHA256.HashData(buffer.AsSpan(0, length));
        string storageKey = Hasher.ToHexStringHash(chunkHash);

        if (GarbageCollectorJob.IsChunkBeingDeleted(storageKey))
        {
            _logger.LogDebug("Chunk {Hash} is being GC'd, waiting...", storageKey);
            await Task.Delay(100, ct);
        }

        var chunk = await _layouts.FindChunkAsync(chunkHash);
        if (chunk is null)
        {
            using var chunkStream = new MemoryStream(buffer, 0, length, writable: false);
            await _storage.WriteAsync(storageKey, chunkStream, new PipelineContext());

            chunk = new Chunk
            {
                Hash = chunkHash,
                SizeBytes = length,
                CompressionAlgorithm = CompressionProcessor.Algorithm
            };
            await _dbContext.Chunks.AddAsync(chunk, ct);
        }
        else if (chunk.GCScheduledAfter.HasValue)
        {
            chunk.GCScheduledAfter = null;
            _dbContext.Chunks.Update(chunk);
        }

        await EnsureOwnershipAsync(chunkHash, userId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return chunk;
    }

    public async Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        using var ms = new MemoryStream(capacity: (int)Math.Min(length, int.MaxValue));
        await stream.CopyToAsync(ms, ct);

        if (ms.Length != length)
        {
            throw new InvalidOperationException("Unexpected stream length.");
        }

        var buffer = ms.GetBuffer();
        return await UpsertChunkAsync(userId, buffer, (int)ms.Length, ct);
    }

    private async Task EnsureOwnershipAsync(byte[] chunkHash, Guid userId, CancellationToken ct)
    {
        var ownershipExists = await _dbContext.ChunkOwnerships
            .AnyAsync(co => co.ChunkHash == chunkHash && co.OwnerId == userId, ct);

        if (!ownershipExists)
        {
            await _dbContext.ChunkOwnerships.AddAsync(new ChunkOwnership
            {
                ChunkHash = chunkHash,
                OwnerId = userId
            }, ct);
        }
    }
}
