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
            // Keep the base metrics meaningful, but per-level breakdown is provided via AggregateMetrics.
            var stopwatch = Stopwatch.StartNew();

            await using var outputStream = new MemoryStream();
            await using var compressor = new CompressionStream(outputStream, level: _levels[0], leaveOpen: true);
            await compressor.WriteAsync(_testData, cancellationToken);

            stopwatch.Stop();
            return PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var resultsByLevel = new Dictionary<int, (double mbps, int compressedBytes)>();

            foreach (var level in _levels)
            {
                var sw = Stopwatch.StartNew();
                using var outputStream = new MemoryStream(capacity: _testData.Length / 4);
                using (var compressor = new CompressionStream(outputStream, level: level, leaveOpen: true))
                {
                    compressor.Write(_testData);
                    compressor.Flush();
                }
                sw.Stop();

                var mbps = PerformanceMetrics.Create(_testData.Length, sw.Elapsed).MegabytesPerSecond;
                resultsByLevel[level] = (mbps, (int)outputStream.Length);
            }

            var dict = new Dictionary<string, object>
            {
                ["Engine"] = "ZstdSharp",
                ["DataType"] = "Compressible Text",
                ["InputSize"] = FormatBytes(_testData.Length),
            };

            foreach (var (level, value) in resultsByLevel.OrderBy(kvp => kvp.Key))
            {
                dict[$"L{level}_Throughput"] = $"{value.mbps:F2} MB/s";
                dict[$"L{level}_Compressed"] = FormatBytes(value.compressedBytes);
                dict[$"L{level}_Ratio"] = $"{(double)_testData.Length / Math.Max(1, value.compressedBytes):F2}x";
            }

            // Provide a simple headline for table sorting (average across levels in this single run)
            var avg = resultsByLevel.Values.Average(v => v.mbps);
            dict["AvgThroughput"] = $"{avg:F2} MB/s";
            dict["MinThroughput"] = $"{resultsByLevel.Values.Min(v => v.mbps):F2} MB/s";
            dict["MaxThroughput"] = $"{resultsByLevel.Values.Max(v => v.mbps):F2} MB/s";
            dict["Iterations"] = metrics.Count;
            dict["DataSize"] = FormatBytes(_configuration.DataSizeBytes);

            return dict;
        }
    }
}
