// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Jobs;
using Cotton.Server.Providers;
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
    SettingsProvider _settingsProvider,
    ILogger<ChunkIngestService> _logger)
    : IChunkIngestService
{
    public async Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, CancellationToken ct = default)
    {
        byte[] chunkHash = SHA256.HashData(buffer.AsSpan(0, length));
        string storageKey = Hasher.ToHexStringHash(chunkHash);

        if (GarbageCollectorJob.IsChunkBeingDeleted(storageKey))
        {
            _logger.LogInformation("Chunk {Hash} is being GC'd, waiting...", storageKey);
            await Task.Delay(100, ct);
        }

        var settings = _settingsProvider.GetServerSettings();

        var chunk = await _layouts.FindChunkAsync(chunkHash);
        bool existsInStorage = await _storage.ExistsAsync(storageKey);

        if (chunk is not null && settings.AllowCrossUserDeduplication && existsInStorage)
        {
            if (chunk.StoredSizeBytes <= 0)
            {
                chunk.StoredSizeBytes = await _storage.GetSizeAsync(storageKey);
                _dbContext.Chunks.Update(chunk);
            }

            if (chunk.GCScheduledAfter.HasValue)
            {
                chunk.GCScheduledAfter = null;
                _dbContext.Chunks.Update(chunk);
            }

            await EnsureOwnershipAsync(chunkHash, userId, ct);
            await _dbContext.SaveChangesAsync(ct);
            return chunk;
        }

        if (!existsInStorage)
        {
            using var chunkStream = new MemoryStream(buffer, 0, length, writable: false);
            await _storage.WriteAsync(storageKey, chunkStream, new PipelineContext());
        }

        long storedSizeBytes = await _storage.GetSizeAsync(storageKey);

        if (chunk is null)
        {
            chunk = new Chunk
            {
                Hash = chunkHash,
                PlainSizeBytes = length,
                StoredSizeBytes = storedSizeBytes,
                CompressionAlgorithm = CompressionProcessor.Algorithm
            };
            await _dbContext.Chunks.AddAsync(chunk, ct);
        }
        else
        {
            bool updated = false;
            if (chunk.GCScheduledAfter.HasValue)
            {
                chunk.GCScheduledAfter = null;
                updated = true;
            }

            if (chunk.PlainSizeBytes <= 0)
            {
                chunk.PlainSizeBytes = length;
                updated = true;
            }

            if (chunk.StoredSizeBytes <= 0)
            {
                chunk.StoredSizeBytes = storedSizeBytes;
                updated = true;
            }

            if (updated)
            {
                _dbContext.Chunks.Update(chunk);
            }
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
