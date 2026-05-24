// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Processors;
using Cotton.Crypto;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for Cotton.Storage AES-GCM encryption throughput.
    /// </summary>
    public sealed class EncryptionBenchmark : BenchmarkBase, IDisposable
    {
        private readonly byte[] _testData;
        private readonly CryptoProcessor _processor;
        private readonly AesGcmStreamCipher _cipher;

        /// <summary>Initializes the benchmark with a fixed measurement configuration.</summary>
        public EncryptionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            // Use mixed data (more realistic than pure random)
            _testData = TestDataGenerator.GenerateMixedData(configuration.DataSizeBytes);

            // Create AesGcmStreamCipher with proper configuration
            var key = new byte[configuration.EncryptionKeySize];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);

            // Create CryptoProcessor from Cotton.Storage
            _processor = new CryptoProcessor(_cipher);
        }

        /// <inheritdoc/>
        public override string Name => "Cotton.Storage AES-GCM Encryption";

        /// <inheritdoc/>
        public override string Description => $"Measures Cotton.Storage AES-GCM throughput with {_configuration.EncryptionThreads} threads";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_testData);
            var outputStream = await _processor.WriteAsync("test-uid", inputStream);
            await outputStream.DisposeAsync();
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_testData);
            var outputStream = await _processor.WriteAsync("test-uid", inputStream);

            // Read all encrypted data
            await using var resultStream = new MemoryStream();
            await outputStream.CopyToAsync(resultStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Implementation"] = "Cotton.Storage.Processors.CryptoProcessor";
            baseMetrics["Cipher"] = "AesGcmStreamCipher";
            baseMetrics["EncryptionThreads"] = _configuration.EncryptionThreads ?? 0;
            baseMetrics["KeySize"] = $"{_configuration.EncryptionKeySize * 8} bits";
            return baseMetrics;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cipher?.Dispose();
        }
    }
}
