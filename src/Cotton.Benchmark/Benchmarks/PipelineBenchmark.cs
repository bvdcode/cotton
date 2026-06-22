// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Crypto;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for the storage pipeline processing chain.
    /// </summary>
    public class PipelineBenchmark : BenchmarkBase, IDisposable
    {
        private readonly byte[] _testData;
        private readonly FileStoragePipeline _pipeline;
        private readonly AesGcmStreamCipher _cipher;

        /// <summary>
        /// Initializes the benchmark with a fixed measurement configuration.
        /// </summary>
        public PipelineBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            // Use compressible JSON data (realistic)
            _testData = TestDataGenerator.GenerateJsonData(configuration.DataSizeBytes);

            // Create AesGcmStreamCipher
            var key = new byte[configuration.EncryptionKeySize];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);

            // Create processors from Cotton.Storage
            var cryptoProcessor = new CryptoProcessor(_cipher);
            var compressionProcessor = new CompressionProcessor(new FixedCompressionLevelProvider(configuration.CompressionLevel));

            // Use in-memory backend for speed (avoiding disk I/O in this test)
            var backend = new InMemoryStorageBackend();

            // Create FileStoragePipeline from Cotton.Storage
            _pipeline = new FileStoragePipeline(
                NullLogger<FileStoragePipeline>.Instance,
                new StaticStorageBackendProvider(backend),
                [cryptoProcessor, compressionProcessor]);
        }

        /// <inheritdoc/>
        public override string Name => "Storage Pipeline (Compression + Encryption)";

        /// <inheritdoc/>
        public override string Description => "Measures the storage pipeline with compression and encryption enabled";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_testData);
            await _pipeline.WriteAsync("test-uid", inputStream);
            Stream outputStream = await _pipeline.ReadAsync("test-uid");
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
            Stream outputStream = await _pipeline.ReadAsync("test-uid");
            await using var resultStream = new MemoryStream();
            await outputStream.CopyToAsync(resultStream, cancellationToken);

            stopwatch.Stop();

            // Count both write and read
            return PerformanceMetrics.Create(_testData.Length * 2, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Pipeline"] = "Cotton.Storage.Pipelines.FileStoragePipeline";
            baseMetrics["Processors"] = "CompressionProcessor + CryptoProcessor";
            baseMetrics["DataType"] = "Compressible JSON";
            baseMetrics["CompressionLevel"] = _configuration.CompressionLevel;
            return baseMetrics;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cipher?.Dispose();
        }
    }
}
