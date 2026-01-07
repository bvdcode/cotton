// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using EasyExtensions.Crypto;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for REAL FileStoragePipeline with full processing chain.
    /// </summary>
    public sealed class PipelineBenchmark : BenchmarkBase, IDisposable
    {
        private readonly byte[] _testData;
        private readonly FileStoragePipeline _pipeline;
        private readonly AesGcmStreamCipher _cipher;
        private readonly InMemoryBackend _backend;

        public PipelineBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            // Use compressible JSON data (realistic)
            _testData = TestDataGenerator.GenerateJsonData(configuration.DataSizeBytes);

            // Create REAL AesGcmStreamCipher
            var key = new byte[configuration.EncryptionKeySize];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);

            // Create REAL processors from Cotton.Storage
            var cryptoProcessor = new CryptoProcessor(_cipher);
            var compressionProcessor = new CompressionProcessor();

            // Use in-memory backend for speed (avoiding disk I/O in this test)
            _backend = new InMemoryBackend();
            var backendProvider = new SimpleBackendProvider(_backend);

            // Create REAL FileStoragePipeline from Cotton.Storage
            _pipeline = new FileStoragePipeline(
                NullLogger<FileStoragePipeline>.Instance,
                backendProvider,
                [cryptoProcessor, compressionProcessor]);
        }

        /// <inheritdoc/>
        public override string Name => "Full Pipeline (Real FileStoragePipeline)";

        /// <inheritdoc/>
        public override string Description => "Tests REAL Cotton.Storage.Pipelines.FileStoragePipeline with Compression + Encryption";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_testData);
            await _pipeline.WriteAsync("test-uid", inputStream);
            var outputStream = await _pipeline.ReadAsync("test-uid");
            await outputStream.DisposeAsync();
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            // Write through pipeline
            await using var inputStream = new MemoryStream(_testData);
            await _pipeline.WriteAsync("test-uid", inputStream);

            // Read back through pipeline
            var outputStream = await _pipeline.ReadAsync("test-uid");
            await using var resultStream = new MemoryStream();
            await outputStream.CopyToAsync(resultStream, cancellationToken);

            stopwatch.Stop();

            // Count both write and read
            return PerformanceMetrics.Create(_testData.Length * 2, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Pipeline"] = "Cotton.Storage.Pipelines.FileStoragePipeline";
            baseMetrics["Processors"] = "CompressionProcessor + CryptoProcessor";
            baseMetrics["DataType"] = "Compressible JSON";
            return baseMetrics;
        }

        public void Dispose()
        {
            _cipher?.Dispose();
        }

        /// <summary>
        /// Simple in-memory backend for testing without disk I/O.
        /// </summary>
        private class InMemoryBackend : IStorageBackend
        {
            private readonly Dictionary<string, byte[]> _storage = [];

            public Task<bool> DeleteAsync(string uid)
            {
                return Task.FromResult(_storage.Remove(uid));
            }

            public Task<bool> ExistsAsync(string uid)
            {
                return Task.FromResult(_storage.ContainsKey(uid));
            }

            public Task<Stream> ReadAsync(string uid)
            {
                if (!_storage.TryGetValue(uid, out var data))
                {
                    throw new FileNotFoundException($"UID not found: {uid}");
                }
                return Task.FromResult<Stream>(new MemoryStream(data));
            }

            public Task WriteAsync(string uid, Stream stream)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                _storage[uid] = ms.ToArray();
                return Task.CompletedTask;
            }
        }

        private class SimpleBackendProvider(IStorageBackend backend) : IStorageBackendProvider
        {
            private readonly IStorageBackend _backend = backend;

            public IStorageBackend GetBackend() => _backend;
        }
    }
}
