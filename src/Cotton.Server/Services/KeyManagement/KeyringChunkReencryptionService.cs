// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Rewrites stored chunks through the current keyring primary key in small, resumable batches.
/// </summary>
public sealed class KeyringChunkReencryptionService(
    CottonDbContext _dbContext,
    IStorageBackendProvider _backendProvider,
    IStoragePipeline _storage,
    KeyringRuntimeState _runtimeState,
    ILogger<KeyringChunkReencryptionService> _logger)
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 500;
    private const int MinimumHeaderBytesForKeyId = 20;
    private static ReadOnlySpan<byte> CurrentMagic => "CTN2"u8;
    private static ReadOnlySpan<byte> LegacyMagic => "CTN1"u8;

    /// <summary>
    /// Re-encrypts one ordered chunk batch and returns the next scan offset.
    /// </summary>
    public async Task<KeyringReencryptChunksResponseDto> ReencryptBatchAsync(
        int offset,
        int limit,
        CancellationToken ct = default)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be zero or greater.");
        }

        int batchLimit = NormalizeLimit(limit);
        KeyringBootstrapResult keyring = _runtimeState.Current
            ?? throw new InvalidOperationException("Keyring v2 is not loaded.");
        int targetKeyId = keyring.State.Primary.ChunkAead;

        int totalChunks = await _dbContext.Chunks.CountAsync(ct);
        List<Chunk> chunks = await _dbContext.Chunks
            .OrderBy(c => c.Hash)
            .Skip(offset)
            .Take(batchLimit)
            .ToListAsync(ct);

        int reencrypted = 0;
        int alreadyCurrent = 0;
        int missing = 0;
        int failed = 0;
        IStorageBackend backend = _backendProvider.GetBackend();

        foreach (Chunk chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            string uid = Hasher.ToHexStringHash(chunk.Hash);

            try
            {
                int? storedKeyId = await TryReadStoredChunkKeyIdAsync(backend, uid, ct);
                if (storedKeyId is null)
                {
                    missing++;
                    continue;
                }

                if (storedKeyId == targetKeyId)
                {
                    alreadyCurrent++;
                    continue;
                }

                await using Stream plaintext = await _storage.ReadAsync(uid);
                await _storage.WriteAsync(
                    uid,
                    plaintext,
                    new PipelineContext { OverwriteExisting = true });

                int? rewrittenKeyId = await TryReadStoredChunkKeyIdAsync(backend, uid, ct);
                if (rewrittenKeyId != targetKeyId)
                {
                    failed++;
                    _logger.LogError(
                        "Chunk {Uid} re-encryption verification failed. Expected key id {TargetKeyId}, got {StoredKeyId}.",
                        uid,
                        targetKeyId,
                        rewrittenKeyId);
                    continue;
                }

                chunk.StoredSizeBytes = await _storage.GetSizeAsync(uid);
                reencrypted++;
            }
            catch (Exception ex) when (IsChunkRewriteFailure(ex))
            {
                failed++;
                _logger.LogWarning(ex, "Failed to re-encrypt chunk {Uid}.", uid);
            }
        }

        if (reencrypted > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        int nextOffset = offset + chunks.Count;
        return new KeyringReencryptChunksResponseDto
        {
            TargetKeyId = targetKeyId,
            Offset = offset,
            NextOffset = nextOffset,
            TotalChunks = totalChunks,
            Scanned = chunks.Count,
            Reencrypted = reencrypted,
            AlreadyCurrent = alreadyCurrent,
            Missing = missing,
            Failed = failed,
            Completed = nextOffset >= totalChunks,
        };
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
        {
            return DefaultLimit;
        }

        return Math.Clamp(limit, 1, MaxLimit);
    }

    private static bool IsChunkRewriteFailure(Exception ex) =>
        ex is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or CryptographicException
            or KeyNotFoundException
            or InvalidOperationException;

    private static async Task<int?> TryReadStoredChunkKeyIdAsync(
        IStorageBackend backend,
        string uid,
        CancellationToken ct)
    {
        try
        {
            await using Stream stream = await backend.ReadAsync(uid);
            byte[] prefix = new byte[MinimumHeaderBytesForKeyId];
            int read = await ReadExactlyUpToAsync(stream, prefix, ct);
            if (read < MinimumHeaderBytesForKeyId)
            {
                throw new InvalidDataException("Encrypted Cotton stream is too short to contain a key id.");
            }

            return ReadKeyId(prefix);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static int ReadKeyId(ReadOnlySpan<byte> prefix)
    {
        ReadOnlySpan<byte> magic = prefix[..4];
        if (!magic.SequenceEqual(CurrentMagic) && !magic.SequenceEqual(LegacyMagic))
        {
            throw new InvalidDataException("Invalid Cotton encrypted stream magic.");
        }

        return BinaryPrimitives.ReadInt32LittleEndian(prefix.Slice(16, 4));
    }

    private static async Task<int> ReadExactlyUpToAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset),
                ct);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }
}
