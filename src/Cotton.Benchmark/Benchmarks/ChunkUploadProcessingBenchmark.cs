// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Crypto;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Data shape used by the chunk upload processing benchmark.
    /// </summary>
    public enum ChunkUploadDataProfile
    {
        /// <summary>Log/document-like data that Zstandard can compress aggressively.</summary>
        CompressibleText,
        /// <summary>Half patterned and half pseudo-random data.</summary>
        MixedContent,
        /// <summary>Random binary data, similar to already-compressed media or archives.</summary>
        RandomBinary
    }

    /// <summary>
    /// Measures the CPU-bound server chunk upload path as one operation.
    /// </summary>
    public class ChunkUploadProcessingBenchmark : BenchmarkBase, IDisposable
    {
        private readonly byte[] _testData;
        private readonly string _dataType;
        private readonly FileStoragePipeline _pipeline;
        private readonly AesGcmStreamCipher _cipher;
        private readonly InMemoryStorageBackend _backend;
        private int _uidCounter;

        /// <summary>Initializes the benchmark with the same processors used by server chunk storage.</summary>
        public ChunkUploadProcessingBenchmark(BenchmarkConfiguration configuration, ChunkUploadDataProfile profile)
            : base(configuration)
        {
            (_testData, _dataType) = CreateTestData(configuration.DataSizeBytes, profile);

            var key = new byte[configuration.EncryptionKeySize];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);

            _backend = new InMemoryStorageBackend();
            _pipeline = new FileStoragePipeline(
                NullLogger<FileStoragePipeline>.Instance,
                new StaticStorageBackendProvider(_backend),
                [new CryptoProcessor(_cipher), new CompressionProcessor(new FixedCompressionLevelProvider(configuration.CompressionLevel))]);
        }

        /// <inheritdoc/>
        public override string Name => $"Chunk Upload Processing - {_dataType} (SHA-256 + Compression + Encryption)";

        /// <inheritdoc/>
        public override string Description => "Measures current server-side chunk upload processing without HTTP, database, or disk latency";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await ProcessChunkAsync(cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            await ProcessChunkAsync(cancellationToken);
            stopwatch.Stop();

            return PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Path"] = "Single-pass upload hash verification + buffer copy + compression + encryption write";
            baseMetrics["StorageBackend"] = "In-memory benchmark backend";
            baseMetrics["ServerHashPasses"] = 1;
            baseMetrics["ReadBack"] = false;
            baseMetrics["DataType"] = _dataType;
            baseMetrics["CompressionLevel"] = _configuration.CompressionLevel;
            return baseMetrics;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cipher.Dispose();
        }

        private static (byte[] Data, string Name) CreateTestData(int sizeBytes, ChunkUploadDataProfile profile)
        {
            return profile switch
            {
                ChunkUploadDataProfile.CompressibleText => (TestDataGenerator.GenerateCompressibleText(sizeBytes), "Compressible text"),
                ChunkUploadDataProfile.MixedContent => (TestDataGenerator.GenerateMixedData(sizeBytes), "Mixed content"),
                ChunkUploadDataProfile.RandomBinary => (TestDataGenerator.GenerateRandomBinary(sizeBytes), "Random binary"),
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported data profile.")
            };
        }

        private async Task ProcessChunkAsync(CancellationToken cancellationToken)
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var uploadStream = new MemoryStream(_testData, writable: false);
            await using var bufferedStream = new MemoryStream(capacity: _testData.Length);
            byte[] rented = ArrayPool<byte>.Shared.Rent(128 * 1024);

            try
            {
                int bytesRead;
                while ((bytesRead = await uploadStream.ReadAsync(rented, cancellationToken)) > 0)
                {
                    hasher.AppendData(rented, 0, bytesRead);
                    await bufferedStream.WriteAsync(rented.AsMemory(0, bytesRead), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }

            int length = checked((int)bufferedStream.Length);
            byte[] buffer = bufferedStream.GetBuffer();
            byte[] storageHash = hasher.GetHashAndReset();
            string storageKey = $"{Convert.ToHexString(storageHash).ToLowerInvariant()}{Interlocked.Increment(ref _uidCounter):x8}";
            try
            {
                await using var chunkStream = new MemoryStream(buffer, 0, length, writable: false);
                await _pipeline.WriteAsync(storageKey, chunkStream, new PipelineContext());
            }
            finally
            {
                await _backend.DeleteAsync(storageKey);
            }
        }
    }
}
