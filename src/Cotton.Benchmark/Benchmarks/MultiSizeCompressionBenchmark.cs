// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using System.Diagnostics;
using ZstdSharp;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Raw ZstdSharp benchmark (independent from Cotton.Storage) that tests multiple compression levels.
    /// </summary>
    public sealed class MultiSizeCompressionBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = TestDataGenerator.GenerateCompressibleText(configuration.DataSizeBytes);
        private readonly int[] _levels = [1, 2, 3, 4, 5];

        /// <inheritdoc/>
        public override string Name => "ZstdSharp (Raw) Levels 1-5";

        /// <inheritdoc/>
        public override string Description => "Tests raw ZstdSharp compression at levels 1..5 (not Cotton.Storage processor)";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            foreach (var level in _levels)
            {
                await using var outputStream = new MemoryStream();
                await using var compressor = new CompressionStream(outputStream, level: level, leaveOpen: true);
                await compressor.WriteAsync(_testData, cancellationToken);
            }
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            long totalBytes = 0;

            foreach (var level in _levels)
            {
                totalBytes += _testData.Length;

                await using var outputStream = new MemoryStream();
                await using var compressor = new CompressionStream(outputStream, level: level, leaveOpen: true);
                await compressor.WriteAsync(_testData, cancellationToken);
            }

            stopwatch.Stop();

            return PerformanceMetrics.Create(totalBytes, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Engine"] = "ZstdSharp";
            baseMetrics["Levels"] = string.Join(",", _levels);
            baseMetrics["DataType"] = "Compressible Text";
            return baseMetrics;
        }
    }
}
