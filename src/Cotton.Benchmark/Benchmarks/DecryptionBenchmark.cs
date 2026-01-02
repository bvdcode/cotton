// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using EasyExtensions.Crypto;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for decryption performance using AES-GCM.
    /// </summary>
    public sealed class DecryptionBenchmark : BenchmarkBase
    {
        private readonly byte[] _encryptedData;
        private readonly int _originalSize;
        private readonly AesGcmStreamCipher _cipher;

        public DecryptionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            var testData = GenerateTestData(configuration.DataSizeBytes);
            _originalSize = testData.Length;

            // Create cipher
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(
                key,
                keyId: 1,
                threads: configuration.EncryptionThreads);

            // Pre-encrypt data for decryption benchmark
            using var inputStream = new MemoryStream(testData);
            var encryptedStream = _cipher.EncryptAsync(inputStream).Result;
            using var outputStream = new MemoryStream();
            encryptedStream.CopyTo(outputStream);
            _encryptedData = outputStream.ToArray();
        }

        /// <inheritdoc/>
        public override string Name => "Decryption (AES-GCM)";

        /// <inheritdoc/>
        public override string Description => $"Tests AES-GCM decryption with {_cipher} threads";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_encryptedData);
            var decryptedStream = await _cipher.DecryptAsync(inputStream);
            await using var outputStream = new MemoryStream();
            await decryptedStream.CopyToAsync(outputStream, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_encryptedData);
            var decryptedStream = await _cipher.DecryptAsync(inputStream);
            await using var outputStream = new MemoryStream();
            await decryptedStream.CopyToAsync(outputStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_originalSize, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["EncryptionThreads"] = _configuration.EncryptionThreads;
            baseMetrics["EncryptedSize"] = FormatBytes(_encryptedData.Length);
            return baseMetrics;
        }
    }
}
