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
    /// Benchmark for Cotton.Storage AES-GCM decryption throughput.
    /// </summary>
    public class DecryptionBenchmark : BenchmarkBase, IDisposable
    {
        private readonly byte[] _encryptedData;
        private readonly int _originalSize;
        private readonly CryptoProcessor _processor;
        private readonly AesGcmStreamCipher _cipher;

        /// <summary>
        /// Initializes the benchmark with a fixed measurement configuration.
        /// </summary>
        public DecryptionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            // Use mixed data
            var testData = TestDataGenerator.GenerateMixedData(configuration.DataSizeBytes);
            _originalSize = testData.Length;

            // Create AesGcmStreamCipher
            var key = new byte[configuration.EncryptionKeySize];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);

            // Create CryptoProcessor
            _processor = new CryptoProcessor(_cipher);

            // Pre-encrypt data using processor
            using var inputStream = new MemoryStream(testData);
            Stream encryptedStream = _processor.WriteAsync("test-uid", inputStream).Result;
            using var outputStream = new MemoryStream();
            encryptedStream.CopyTo(outputStream);
            _encryptedData = outputStream.ToArray();
        }

        /// <inheritdoc/>
        public override string Name => "Cotton.Storage AES-GCM Decryption";

        /// <inheritdoc/>
        public override string Description => $"Measures Cotton.Storage AES-GCM throughput with {_configuration.EncryptionThreads} threads";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_encryptedData);
            Stream outputStream = await _processor.ReadAsync("test-uid", inputStream);
            await outputStream.DisposeAsync();
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_encryptedData);
            Stream outputStream = await _processor.ReadAsync("test-uid", inputStream);

            // Read all decrypted data
            await using var resultStream = new MemoryStream();
            await outputStream.CopyToAsync(resultStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_originalSize, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Implementation"] = "Cotton.Storage.Processors.CryptoProcessor";
            baseMetrics["Cipher"] = "AesGcmStreamCipher";
            baseMetrics["EncryptionThreads"] = _configuration.EncryptionThreads ?? 0;
            baseMetrics["EncryptedSize"] = FormatBytes(_encryptedData.Length);
            return baseMetrics;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cipher?.Dispose();
        }
    }
}
