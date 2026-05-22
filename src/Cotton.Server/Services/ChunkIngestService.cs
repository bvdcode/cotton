// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

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

/// <summary>
/// Coordinates chunk ingest.
/// </summary>
public class ChunkIngestService(
    CottonDbContext _dbContext,
    ILayoutService _layouts,
    IStoragePipeline _storage,
    StoragePressureGuard _storagePressure,
    SettingsProvider _settingsProvider,
    ILogger<ChunkIngestService> _logger)
    : IChunkIngestService
{
    private const int GcWaitStepMs = 100;
    private const int GcWaitMaxMs = 30_000;

    /// <summary>
    /// Stores a chunk if it does not already exist and records ownership.
    /// </summary>
    public async Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, CancellationToken ct = default)
    {
        byte[] chunkHash = SHA256.HashData(buffer.AsSpan(0, length));
        string storageKey = Hasher.ToHexStringHash(chunkHash);

        await WaitForGarbageCollectionAsync(storageKey, ct);

        var settings = _settingsProvider.GetServerSettings();
        var chunk = await _layouts.FindChunkAsync(chunkHash);
        bool existsInStorage = await _storage.ExistsAsync(storageKey);

        Chunk? reusedChunk = await TryReuseDeduplicatedChunkAsync(
            chunk,
            settings.AllowCrossUserDeduplication,
            existsInStorage,
            storageKey,
            chunkHash,
            userId,
            ct);
        if (reusedChunk is not null)
        {
            return reusedChunk;
        }

        if (!existsInStorage)
        {
            await WriteChunkAsync(storageKey, buffer, length, ct);
        }

        long storedSizeBytes = await _storage.GetSizeAsync(storageKey);
        chunk = await UpsertChunkMetadataAsync(chunk, chunkHash, length, storedSizeBytes, ct);

        await EnsureOwnershipAsync(chunkHash, userId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return chunk;
    }

    private async Task WaitForGarbageCollectionAsync(string storageKey, CancellationToken ct)
    {
        int waitedMs = 0;
        while (GarbageCollectorJob.IsChunkBeingDeleted(storageKey) && waitedMs < GcWaitMaxMs)
        {
            _logger.LogInformation("Chunk {Hash} is being GC'd, waiting...", storageKey);
            await Task.Delay(GcWaitStepMs, ct);
            waitedMs += GcWaitStepMs;
        }

        if (GarbageCollectorJob.IsChunkBeingDeleted(storageKey))
        {
            throw new InvalidOperationException($"Chunk {storageKey} is currently being garbage collected. Please retry.");
        }
    }

    private async Task<Chunk?> TryReuseDeduplicatedChunkAsync(
        Chunk? chunk,
        bool allowCrossUserDeduplication,
        bool existsInStorage,
        string storageKey,
        byte[] chunkHash,
        Guid userId,
        CancellationToken ct)
    {
        if (chunk is null || !allowCrossUserDeduplication || !existsInStorage)
        {
            return null;
        }

        await RefreshStoredChunkMetadataAsync(chunk, storageKey);
        await EnsureOwnershipAsync(chunkHash, userId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return chunk;
    }

    private async Task RefreshStoredChunkMetadataAsync(Chunk chunk, string storageKey)
    {
        bool updated = false;
        if (chunk.StoredSizeBytes <= 0)
        {
            chunk.StoredSizeBytes = await _storage.GetSizeAsync(storageKey);
            updated = true;
        }

        if (chunk.GCScheduledAfter.HasValue)
        {
            chunk.GCScheduledAfter = null;
            updated = true;
        }

        if (updated)
        {
            _dbContext.Chunks.Update(chunk);
        }
    }

    private async Task WriteChunkAsync(string storageKey, byte[] buffer, int length, CancellationToken ct)
    {
        using var writeReservation = await _storagePressure.ReserveWriteAsync(length, ct);
        using var chunkStream = new MemoryStream(buffer, 0, length, writable: false);
        await _storage.WriteAsync(storageKey, chunkStream, new PipelineContext());
        writeReservation.Commit();
    }

    private async Task<Chunk> UpsertChunkMetadataAsync(
        Chunk? chunk,
        byte[] chunkHash,
        int plainSizeBytes,
        long storedSizeBytes,
        CancellationToken ct)
    {
        if (chunk is null)
        {
            return await AddChunkMetadataAsync(chunkHash, plainSizeBytes, storedSizeBytes, ct);
        }

        UpdateChunkMetadata(chunk, plainSizeBytes, storedSizeBytes);
        return chunk;
    }

    private async Task<Chunk> AddChunkMetadataAsync(
        byte[] chunkHash,
        int plainSizeBytes,
        long storedSizeBytes,
        CancellationToken ct)
    {
        var chunk = new Chunk
        {
            Hash = chunkHash,
            PlainSizeBytes = plainSizeBytes,
            StoredSizeBytes = storedSizeBytes,
            CompressionAlgorithm = CompressionProcessor.Algorithm
        };
        await _dbContext.Chunks.AddAsync(chunk, ct);
        return chunk;
    }

    private void UpdateChunkMetadata(Chunk chunk, int plainSizeBytes, long storedSizeBytes)
    {
        bool updated = false;
        updated |= ClearGcSchedule(chunk);
        updated |= SetPlainSizeIfMissing(chunk, plainSizeBytes);
        updated |= SetStoredSizeIfMissing(chunk, storedSizeBytes);

        if (updated)
        {
            _dbContext.Chunks.Update(chunk);
        }
    }

    private static bool ClearGcSchedule(Chunk chunk)
    {
        if (!chunk.GCScheduledAfter.HasValue)
        {
            return false;
        }

        chunk.GCScheduledAfter = null;
        return true;
    }

    private static bool SetPlainSizeIfMissing(Chunk chunk, int plainSizeBytes)
    {
        if (chunk.PlainSizeBytes > 0)
        {
            return false;
        }

        chunk.PlainSizeBytes = plainSizeBytes;
        return true;
    }

    private static bool SetStoredSizeIfMissing(Chunk chunk, long storedSizeBytes)
    {
        if (chunk.StoredSizeBytes > 0)
        {
            return false;
        }

        chunk.StoredSizeBytes = storedSizeBytes;
        return true;
    }

    /// <summary>
    /// Stores a chunk if it does not already exist and records ownership.
    /// </summary>
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
