// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Models;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Measures the active storage pipeline with a synthetic 64 MiB blob, without touching user files.
    /// </summary>
    public class StoragePipelineProbeService(
        IStoragePipeline _storage,
        ILogger<StoragePipelineProbeService> _logger)
    {
        /// <summary>
        /// Size of the synthetic probe payload (64 MiB), large enough to produce stable throughput measurements.
        /// </summary>
        public const int PayloadSizeBytes = 64 * 1024 * 1024;

        private static readonly SemaphoreSlim ProbeLock = new(1, 1);
        private const double BytesPerMebibyte = 1024.0 * 1024.0;

        /// <summary>
        /// Runs one warmup iteration and one measured iteration through the real storage pipeline.
        /// </summary>
        public async Task<StoragePipelineProbeResult> RunAsync(string storageBackend, CancellationToken cancellationToken)
        {
            await ProbeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                StoragePipelineProbeIteration warmup = await RunIterationAsync(isWarmup: true, cancellationToken).ConfigureAwait(false);
                StoragePipelineProbeIteration measured = await RunIterationAsync(isWarmup: false, cancellationToken).ConfigureAwait(false);

                return new StoragePipelineProbeResult
                {
                    CompletedAt = DateTimeOffset.UtcNow,
                    PayloadSizeBytes = PayloadSizeBytes,
                    StorageBackend = storageBackend,
                    Warmup = warmup,
                    Measured = measured,
                };
            }
            finally
            {
                ProbeLock.Release();
            }
        }

        private async Task<StoragePipelineProbeIteration> RunIterationAsync(bool isWarmup, CancellationToken cancellationToken)
        {
            byte[] payload = RandomNumberGenerator.GetBytes(PayloadSizeBytes);
            byte[] expectedHash = SHA256.HashData(payload);
            string uid = CreateProbeUid();
            PipelineContext context = new()
            {
                FileSizeBytes = payload.Length,
                ChunkLengths = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [uid] = payload.Length,
                },
            };

            var roundtrip = Stopwatch.StartNew();
            var writeStopwatch = Stopwatch.StartNew();
            long storedSizeBytes = 0;
            try
            {
                await using (var input = new MemoryStream(payload, writable: false))
                {
                    await _storage.WriteAsync(uid, input, context).ConfigureAwait(false);
                }

                writeStopwatch.Stop();
                storedSizeBytes = await _storage.GetSizeAsync(uid).ConfigureAwait(false);

                var readStopwatch = Stopwatch.StartNew();
                await using Stream output = await _storage.ReadAsync(uid, context).ConfigureAwait(false);
                (long readBytes, byte[] actualHash) = await ReadAndHashAsync(output, cancellationToken).ConfigureAwait(false);
                readStopwatch.Stop();
                roundtrip.Stop();

                if (readBytes != payload.Length || !CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                {
                    throw new InvalidOperationException("Storage pipeline probe read back different bytes than it wrote.");
                }

                return new StoragePipelineProbeIteration
                {
                    IsWarmup = isWarmup,
                    WriteMilliseconds = writeStopwatch.Elapsed.TotalMilliseconds,
                    ReadMilliseconds = readStopwatch.Elapsed.TotalMilliseconds,
                    RoundtripMilliseconds = roundtrip.Elapsed.TotalMilliseconds,
                    WriteMebibytesPerSecond = ToMebibytesPerSecond(payload.Length, writeStopwatch.Elapsed),
                    ReadMebibytesPerSecond = ToMebibytesPerSecond(payload.Length, readStopwatch.Elapsed),
                    StoredSizeBytes = storedSizeBytes,
                };
            }
            finally
            {
                await DeleteProbeBlobAsync(uid).ConfigureAwait(false);
            }
        }

        private async Task DeleteProbeBlobAsync(string uid)
        {
            try
            {
                await _storage.DeleteAsync(uid).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Storage pipeline probe could not delete temporary blob {Uid}.", uid);
            }
        }

        private static string CreateProbeUid()
        {
            byte[] entropy = RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexString(SHA256.HashData(entropy)).ToLowerInvariant();
        }

        private static async Task<(long BytesRead, byte[] Hash)> ReadAndHashAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long totalBytes = 0;
            try
            {
                while (true)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    hasher.AppendData(buffer, 0, read);
                    totalBytes += read;
                }

                return (totalBytes, hasher.GetHashAndReset());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static double ToMebibytesPerSecond(long bytes, TimeSpan elapsed)
        {
            return elapsed.TotalSeconds <= 0
                ? 0
                : bytes / BytesPerMebibyte / elapsed.TotalSeconds;
        }
    }
}
