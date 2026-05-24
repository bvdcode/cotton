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
using Npgsql;
using System.Buffers;
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
        return await UpsertChunkAsync(userId, buffer, length, chunkHash, ct);
    }

    private async Task<Chunk> UpsertChunkAsync(Guid userId, byte[] buffer, int length, byte[] chunkHash, CancellationToken ct)
    {
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
        return await SaveChunkUpsertAsync(chunk, chunkHash, storageKey, userId, ct);
    }

    private async Task<Chunk> SaveChunkUpsertAsync(
        Chunk chunk,
        byte[] chunkHash,
        string storageKey,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return chunk;
        }
        catch (DbUpdateException ex) when (IsConcurrentChunkUpsertConflict(ex))
        {
            _logger.LogDebug(ex, "Chunk {Hash} metadata was upserted concurrently, reloading", storageKey);
            DetachPendingChunkUpsert(chunkHash, userId);
        }

        Chunk existing = await LoadExistingChunkAsync(chunkHash, storageKey, ct);
        await EnsureOwnershipAsync(chunkHash, userId, ct);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsConcurrentChunkUpsertConflict(ex))
        {
            _logger.LogDebug(ex, "Chunk {Hash} ownership was upserted concurrently, reusing existing metadata", storageKey);
            DetachPendingChunkUpsert(chunkHash, userId);
        }

        return existing;
    }

    private async Task<Chunk> LoadExistingChunkAsync(byte[] chunkHash, string storageKey, CancellationToken ct)
    {
        return await _dbContext.Chunks.FirstOrDefaultAsync(c => c.Hash == chunkHash, ct)
            ?? throw new InvalidOperationException($"Chunk {storageKey} was inserted concurrently but could not be reloaded.");
    }

    private void DetachPendingChunkUpsert(byte[] chunkHash, Guid userId)
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries().ToArray())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (entry.Entity is Chunk chunk && chunk.Hash.SequenceEqual(chunkHash))
            {
                entry.State = EntityState.Detached;
                continue;
            }

            if (entry.Entity is ChunkOwnership ownership
                && ownership.OwnerId == userId
                && ownership.ChunkHash.SequenceEqual(chunkHash))
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static bool IsConcurrentChunkUpsertConflict(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            TableName: "chunks" or "chunk_ownerships"
        };
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
    public async Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, byte[] expectedHash, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(expectedHash);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (expectedHash.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException("Expected hash must be a SHA-256 digest.", nameof(expectedHash));
        }

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var ms = new MemoryStream(capacity: checked((int)Math.Min(length, int.MaxValue)));
        byte[] rented = ArrayPool<byte>.Shared.Rent(128 * 1024);
        long totalBytesRead = 0;

        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(rented, ct)) > 0)
            {
                totalBytesRead += bytesRead;
                if (totalBytesRead > length)
                {
                    throw new InvalidOperationException("Unexpected stream length.");
                }

                hasher.AppendData(rented, 0, bytesRead);
                await ms.WriteAsync(rented.AsMemory(0, bytesRead), ct);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        if (totalBytesRead != length)
        {
            throw new InvalidOperationException("Unexpected stream length.");
        }

        byte[] computedHash = hasher.GetHashAndReset();
        if (!CryptographicOperations.FixedTimeEquals(computedHash, expectedHash))
        {
            throw new InvalidDataException("Hash mismatch: the provided hash does not match the uploaded file.");
        }

        return await UpsertChunkAsync(userId, ms.GetBuffer(), (int)ms.Length, computedHash, ct);
    }

    /// <summary>
    /// Stores a chunk from a stream when no caller-provided hash is available.
    /// </summary>
    public async Task<Chunk> UpsertChunkAsync(Guid userId, Stream stream, long length, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        using var ms = new MemoryStream(capacity: checked((int)Math.Min(length, int.MaxValue)));
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
