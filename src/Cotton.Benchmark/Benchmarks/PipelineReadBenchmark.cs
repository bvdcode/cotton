// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Crypto;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    public class PipelineReadBenchmark : BenchmarkBase, IDisposable
    {
        private readonly byte[] _testData;
        private readonly FileStoragePipeline _pipeline;
        private readonly AesGcmStreamCipher _cipher;
        private readonly InMemoryStorageBackend _backend;

        public PipelineReadBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            _testData = TestDataGenerator.GenerateMixedData(configuration.DataSizeBytes);

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

        public override string Name => "Storage Pipeline Read (Decryption + Decompression)";

        public override string Description => "Measures the storage pipeline read path without SHA-256 hashing";

        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await ReadOnceAsync(measure: false, cancellationToken).ConfigureAwait(false);
        }

        protected override Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            return ReadOnceAsync(measure: true, cancellationToken);
        }

        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Pipeline"] = "Cotton.Storage.Pipelines.FileStoragePipeline";
            baseMetrics["Processors"] = "CryptoProcessor + CompressionProcessor";
            baseMetrics["Path"] = "Backend read + decryption + decompression";
            baseMetrics["StorageBackend"] = "In-memory benchmark backend";
            baseMetrics["DataType"] = "Mixed content";
            baseMetrics["CompressionLevel"] = _configuration.CompressionLevel;
            baseMetrics["IncludesHashing"] = false;
            return baseMetrics;
        }

        public void Dispose()
        {
            _cipher.Dispose();
        }

        private async Task<PerformanceMetrics> ReadOnceAsync(bool measure, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string storageKey = CreateStorageKey();
            await using (var inputStream = new MemoryStream(_testData, writable: false))
            {
                await _pipeline.WriteAsync(storageKey, inputStream, new PipelineContext()).ConfigureAwait(false);
            }

            Stopwatch stopwatch = new();
            try
            {
                if (measure)
                {
                    stopwatch.Start();
                }

                await using Stream outputStream = await _pipeline.ReadAsync(storageKey, new PipelineContext()).ConfigureAwait(false);
                await using var resultStream = new MemoryStream(capacity: _testData.Length);
                await outputStream.CopyToAsync(resultStream, cancellationToken).ConfigureAwait(false);

                if (measure)
                {
                    stopwatch.Stop();
                }
            }
            finally
            {
                await _backend.DeleteAsync(storageKey).ConfigureAwait(false);
            }

            return PerformanceMetrics.Create(
                _testData.Length,
                measure ? stopwatch.Elapsed : TimeSpan.Zero);
        }

        private static string CreateStorageKey()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
