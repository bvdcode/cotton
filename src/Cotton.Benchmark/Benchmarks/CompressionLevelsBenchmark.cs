// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using System.Diagnostics;
using ZstdSharp;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for comparing Zstd compression levels by throughput and compression ratio.
    /// </summary>
    public sealed class CompressionLevelsBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = TestDataGenerator.GenerateCompressibleText(configuration.DataSizeBytes);

        // Sweep representative Zstd levels across fast, balanced, and high-compression settings.
        private readonly int[] _levels = [-5, -1, 1, 3, 9, 15, 19, 22];

        /// <inheritdoc/>
        public override string Name => "ZstdSharp Extreme Level Sweep (-5..22)";

        /// <inheritdoc/>
        public override string Description => "Measures direct ZstdSharp throughput and compression ratio across levels -5..22";

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
            // Use the first configured level for base metrics
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
            var resultsByLevel = new Dictionary<int, LevelResult>();

            // Collect one measured sample per configured level
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

                var throughput = PerformanceMetrics.Create(_testData.Length, sw.Elapsed).MegabytesPerSecond;
                var compressedSize = (int)outputStream.Length;
                var ratio = (double)_testData.Length / Math.Max(1, compressedSize);
                var reductionPercent = (1 - ((double)compressedSize / _testData.Length)) * 100;

                resultsByLevel[level] = new LevelResult
                {
                    Level = level,
                    ThroughputMBps = throughput,
                    CompressedSize = compressedSize,
                    CompressionRatio = ratio,
                    ReductionPercent = reductionPercent,
                    TimeMs = sw.Elapsed.TotalMilliseconds
                };
            }

            // Print an operator-readable comparison table
            PrintDetailedComparison(resultsByLevel);

            // Build metrics dictionary
            var dict = new Dictionary<string, object>
            {
                ["Engine"] = "ZstdSharp (Extreme Levels)",
                ["DataType"] = "Compressible Text",
                ["InputSize"] = FormatBytes(_testData.Length),
                ["LevelsTested"] = string.Join(", ", _levels)
            };

            // Add per-level metrics
            foreach (var (level, result) in resultsByLevel.OrderBy(kvp => kvp.Key))
            {
                var levelNote = GetLevelNote(level);
                dict[$"L{level}_Throughput"] = $"{result.ThroughputMBps:F2} MB/s";
                dict[$"L{level}_Compressed"] = FormatBytes(result.CompressedSize);
                dict[$"L{level}_Ratio"] = $"{result.CompressionRatio:F2}x";
                dict[$"L{level}_Reduction"] = $"{result.ReductionPercent:F1}%";
                dict[$"L{level}_Note"] = levelNote;
            }

            // Calculate boundary summaries
            var fastest = resultsByLevel.Values.OrderByDescending(r => r.ThroughputMBps).First();
            var bestCompression = resultsByLevel.Values.OrderByDescending(r => r.CompressionRatio).First();

            dict["FastestLevel"] = $"Level {fastest.Level} ({fastest.ThroughputMBps:F2} MB/s)";
            dict["BestCompressionLevel"] = $"Level {bestCompression.Level} ({bestCompression.CompressionRatio:F2}x)";
            dict["SpeedDifference"] = $"{(resultsByLevel[-5].ThroughputMBps / resultsByLevel[22].ThroughputMBps):F2}x (L-5 vs L22)";
            dict["CompressionDifference"] = $"{(resultsByLevel[22].CompressionRatio / resultsByLevel[-5].CompressionRatio):F2}x (L22 vs L-5)";
            dict["NegativeVsPositive"] = $"{(resultsByLevel[-5].ThroughputMBps / resultsByLevel[1].ThroughputMBps):F2}x (L-5 vs L1)";

            // Standard metrics for table
            var avgThroughput = resultsByLevel.Values.Average(r => r.ThroughputMBps);
            dict["AvgThroughput"] = $"{avgThroughput:F2} MB/s";
            dict["MinThroughput"] = $"{resultsByLevel.Values.Min(r => r.ThroughputMBps):F2} MB/s";
            dict["MaxThroughput"] = $"{resultsByLevel.Values.Max(r => r.ThroughputMBps):F2} MB/s";
            dict["Iterations"] = metrics.Count;
            dict["DataSize"] = FormatBytes(_configuration.DataSizeBytes);
            AddMemoryMetrics(dict, metrics);

            return dict;
        }

        private static void PrintDetailedComparison(Dictionary<int, LevelResult> results)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(new string('-', 100));
            Console.WriteLine(" Level |  Throughput  |   Time   |  Compressed  | Reduction |  Ratio  |  Time vs L-5  |  Category");
            Console.WriteLine(new string('-', 100));

            var baseline = results[-5];

            foreach (var (level, result) in results.OrderBy(kvp => kvp.Key))
            {
                var speedVsBaseline = level == -5
                    ? "baseline"
                    : $"{(baseline.ThroughputMBps / result.ThroughputMBps):F2}x slower";

                var note = GetLevelNote(level);

                Console.WriteLine(
                    $"   {level,2}  | {result.ThroughputMBps,9:F2} MB/s | {result.TimeMs,6:F1} ms | {FormatBytes(result.CompressedSize),12} | {result.ReductionPercent,6:F1}%  | {result.CompressionRatio,5:F2}x | {speedVsBaseline,-13} | {note}");
            }

            Console.WriteLine(new string('-', 100));

            Console.ForegroundColor = ConsoleColor.Cyan;
            var speedRatio = baseline.ThroughputMBps / results[22].ThroughputMBps;
            var compressionRatio = results[22].CompressionRatio / baseline.CompressionRatio;
            var fastLevelRatio = results[-5].ThroughputMBps / results[1].ThroughputMBps;
            Console.WriteLine();
            Console.WriteLine("Derived metrics:");
            Console.WriteLine($"   Level -5 throughput vs level 22: {speedRatio:F1}x");
            Console.WriteLine($"   Level 22 compression ratio vs level -5: {compressionRatio:F2}x");
            Console.WriteLine($"   Level -5 throughput vs level 1: {fastLevelRatio:F2}x");
            Console.WriteLine($"   Level 3 sample: {results[3].ThroughputMBps:F0} MB/s, {results[3].CompressionRatio:F2}x compression ratio");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static string GetLevelNote(int level)
        {
            return level switch
            {
                -5 => "Fastest sample",
                -1 => "Fast sample",
                1 => "Low compression",
                3 => "Default level",
                9 => "Mid-range compression",
                15 => "High compression",
                19 => "Very high compression",
                22 => "Maximum configured level",
                _ => ""
            };
        }

        private sealed class LevelResult
        {
            public required int Level { get; init; }
            public required double ThroughputMBps { get; init; }
            public required int CompressedSize { get; init; }
            public required double CompressionRatio { get; init; }
            public required double ReductionPercent { get; init; }
            public required double TimeMs { get; init; }
        }
    }
}

