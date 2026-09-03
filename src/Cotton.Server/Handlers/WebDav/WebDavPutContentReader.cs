// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using System.Buffers;
using System.Security.Cryptography;

namespace Cotton.Server.Handlers.WebDav
{
    public class WebDavPutContentReader(
        SettingsProvider _settings,
        IChunkIngestService _chunkIngest,
        ILogger<WebDavPutContentReader> _logger)
    {
        public async Task<(WebDavPutContent? Content, WebDavPutFileResult? Error)> ReadAsync(
            WebDavPutFileRequest request,
            CancellationToken cancellationToken)
        {
            List<Chunk> chunks;
            byte[] fileHash;
            try
            {
                (chunks, fileHash) = await ReadChunksAsync(request.Content, request.UserId, cancellationToken);
            }
            catch (StoragePressureException ex)
            {
                _logger.LogWarning(
                    ex,
                    "WebDAV PUT rejected because storage free space is below the configured reserve. Path: {Path}, User: {UserId}",
                    request.Path,
                    request.UserId);
                return (null, Fail(WebDavPutFileError.StoragePressure));
            }

            long totalBytes = chunks.Sum(chunk => chunk.PlainSizeBytes);
            if (request.ContentLength is > 0 && (totalBytes == 0 || totalBytes != request.ContentLength.Value))
            {
                _logger.LogWarning(
                    "WebDAV PUT aborted/truncated: expected length {Expected}, got {Actual} bytes. Path: {Path}, User: {UserId}",
                    request.ContentLength.Value,
                    totalBytes,
                    request.Path,
                    request.UserId);
                return (null, Fail(WebDavPutFileError.UploadAborted));
            }

            if (totalBytes == 0)
            {
                if (request.ContentLength == 0)
                {
                    chunks = [await _chunkIngest.UpsertChunkAsync(request.UserId, [], 0, cancellationToken)];
                    fileHash = Hasher.HashData([]);
                }
                else
                {
                    _logger.LogWarning(
                        "WebDAV PUT got 0 bytes but Content-Length was {ContentLength}. Treating as aborted. Path: {Path}, User: {UserId}",
                        request.ContentLength,
                        request.Path,
                        request.UserId);
                    return (null, Fail(WebDavPutFileError.UploadAborted));
                }
            }

            return (new WebDavPutContent(chunks, fileHash, totalBytes), null);
        }

        private async Task<(List<Chunk> Chunks, byte[] FileHash)> ReadChunksAsync(
            Stream input,
            Guid userId,
            CancellationToken cancellationToken)
        {
            int chunkSize = _settings.GetServerSettings().MaxChunkSizeBytes;
            List<Chunk> chunks = [];
            using IncrementalHash fileHasher = IncrementalHash.CreateHash(Hasher.SupportedHashAlgorithmName);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            try
            {
                int bytesRead;
                while ((bytesRead = await ReadExactlyAsync(input, buffer, chunkSize, cancellationToken)) > 0)
                {
                    fileHasher.AppendData(buffer, 0, bytesRead);
                    chunks.Add(await _chunkIngest.UpsertChunkAsync(userId, buffer, bytesRead, cancellationToken));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return (chunks, fileHasher.GetHashAndReset());
        }

        private static async Task<int> ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            int count,
            CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, count - totalRead),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        private static WebDavPutFileResult Fail(WebDavPutFileError error)
        {
            return new WebDavPutFileResult(false, false, error);
        }
    }
}
