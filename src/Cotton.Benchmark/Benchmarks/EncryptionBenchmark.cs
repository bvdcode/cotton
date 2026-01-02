// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using EasyExtensions.Crypto;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for encryption performance using AES-GCM.
    /// </summary>
    public sealed class EncryptionBenchmark : BenchmarkBase
    {
        private readonly byte[] _testData;
        private readonly AesGcmStreamCipher _cipher;

        public EncryptionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            _testData = GenerateTestData(configuration.DataSizeBytes);

            // Create encryption cipher
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);
        }

        /// <inheritdoc/>
        public override string Name => "Encryption (AES-GCM)";

        /// <inheritdoc/>
        public override string Description => $"Tests AES-GCM encryption with {_cipher} threads";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_testData);
            var encryptedStream = await _cipher.EncryptAsync(inputStream);
            await using var outputStream = new MemoryStream();
            await encryptedStream.CopyToAsync(outputStream, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_testData);
            var encryptedStream = await _cipher.EncryptAsync(inputStream);
            await using var outputStream = new MemoryStream();
            await encryptedStream.CopyToAsync(outputStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["EncryptionThreads"] = _configuration.EncryptionThreads;
            baseMetrics["CipherChunkSize"] = FormatBytes(_configuration.CipherChunkSizeBytes);
            return baseMetrics;
        }
    }
}
