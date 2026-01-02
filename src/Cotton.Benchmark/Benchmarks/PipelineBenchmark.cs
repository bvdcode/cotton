// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Processors;
using EasyExtensions.Crypto;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for full storage pipeline (compression + encryption).
    /// </summary>
    public sealed class PipelineBenchmark : BenchmarkBase
    {
        private readonly byte[] _testData;
        private readonly IStorageProcessor[] _processors;

        public PipelineBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            _testData = GenerateTestData(configuration.DataSizeBytes);

            // Create cipher
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);

            // Create processors (order matters!)
            _processors =
            [
                new CryptoProcessor(cipher),
                new CompressionProcessor()
            ];
        }

        /// <inheritdoc/>
        public override string Name => "Full Pipeline (Compress + Encrypt)";

        /// <inheritdoc/>
        public override string Description => "Tests complete storage pipeline with compression and encryption";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            // Write (compress then encrypt)
            Stream currentStream = new MemoryStream(_testData);
            foreach (var processor in _processors.OrderByDescending(p => p.Priority))
            {
                currentStream = await processor.WriteAsync("test-uid", currentStream);
            }

            // Read (decrypt then decompress)
            foreach (var processor in _processors.OrderBy(p => p.Priority))
            {
                currentStream = await processor.ReadAsync("test-uid", currentStream);
            }

            await using var outputStream = new MemoryStream();
            await currentStream.CopyToAsync(outputStream, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            // Write phase
            Stream currentStream = new MemoryStream(_testData);
            foreach (var processor in _processors.OrderByDescending(p => p.Priority))
            {
                currentStream = await processor.WriteAsync("test-uid", currentStream);
            }

            var writeTime = stopwatch.Elapsed;

            // Read phase
            foreach (var processor in _processors.OrderBy(p => p.Priority))
            {
                currentStream = await processor.ReadAsync("test-uid", currentStream);
            }

            await using var outputStream = new MemoryStream();
            await currentStream.CopyToAsync(outputStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_testData.Length * 2, stopwatch.Elapsed); // Count both write and read
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["ProcessorCount"] = _processors.Length;
            baseMetrics["Pipeline"] = "Compression ? Encryption";
            return baseMetrics;
        }
    }
}
