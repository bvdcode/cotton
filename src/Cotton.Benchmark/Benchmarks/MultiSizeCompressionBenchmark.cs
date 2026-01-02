// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using System.Diagnostics;
using ZstdSharp;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark that tests multiple data sizes for compression.
    /// </summary>
    public sealed class MultiSizeCompressionBenchmark : BenchmarkBase
    {
        private readonly int[] _dataSizes = [1024, 64 * 1024, 1024 * 1024, 10 * 1024 * 1024]; // 1KB, 64KB, 1MB, 10MB
        private readonly int _compressionLevel;

        public MultiSizeCompressionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            _compressionLevel = configuration.CompressionLevel;
        }

        /// <inheritdoc/>
        public override string Name => "Multi-Size Compression";

        /// <inheritdoc/>
        public override string Description => "Tests compression with varying data sizes";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            foreach (var size in _dataSizes)
            {
                var testData = GenerateTestData(size);
                await using var outputStream = new MemoryStream();
                await using (var compressor = new CompressionStream(outputStream, level: _compressionLevel, leaveOpen: true))
                {
                    await compressor.WriteAsync(testData, cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            long totalBytes = 0;

            foreach (var size in _dataSizes)
            {
                var testData = GenerateTestData(size);
                totalBytes += testData.Length;

                await using var outputStream = new MemoryStream();
                await using (var compressor = new CompressionStream(outputStream, level: _compressionLevel, leaveOpen: true))
                {
                    await compressor.WriteAsync(testData, cancellationToken);
                }
            }

            stopwatch.Stop();

            return PerformanceMetrics.Create(totalBytes, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["DataSizes"] = string.Join(", ", _dataSizes.Select(s => FormatBytes(s)));
            baseMetrics["CompressionLevel"] = _compressionLevel;
            return baseMetrics;
        }
    }
}
