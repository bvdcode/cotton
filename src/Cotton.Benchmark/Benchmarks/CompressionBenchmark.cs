// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using System.Diagnostics;
using ZstdSharp;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for compression performance using Zstd.
    /// </summary>
    public sealed class CompressionBenchmark : BenchmarkBase
    {
        private readonly byte[] _testData;
        private readonly int _compressionLevel;

        public CompressionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            _testData = GenerateTestData(configuration.DataSizeBytes);
            _compressionLevel = configuration.CompressionLevel;
        }

        /// <inheritdoc/>
        public override string Name => "Compression (Zstd)";

        /// <inheritdoc/>
        public override string Description => $"Tests Zstd compression at level {_compressionLevel}";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var outputStream = new MemoryStream();
            await using (var compressor = new CompressionStream(outputStream, level: _compressionLevel, leaveOpen: true))
            {
                await compressor.WriteAsync(_testData, cancellationToken);
            }
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var outputStream = new MemoryStream();
            await using (var compressor = new CompressionStream(outputStream, level: _compressionLevel, leaveOpen: true))
            {
                await compressor.WriteAsync(_testData, cancellationToken);
            }

            stopwatch.Stop();

            return PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["CompressionLevel"] = _compressionLevel;
            return baseMetrics;
        }
    }
}
