// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using Cotton.Benchmark.Models;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Base class for all benchmarks with common functionality.
    /// </summary>
    public abstract class BenchmarkBase(BenchmarkConfiguration configuration) : IBenchmark
    {
        /// <summary>Benchmark configuration shared by warmup and measured iterations.</summary>
        protected readonly BenchmarkConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract string Description { get; }

        /// <inheritdoc/>
        public async Task<IBenchmarkResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Warmup
                for (int i = 0; i < _configuration.WarmupIterations; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ExecuteIterationAsync(cancellationToken);
                }

                // Actual measurement
                var metrics = new List<PerformanceMetrics>();
                for (int i = 0; i < _configuration.MeasuredIterations; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var iterationMetrics = await MeasureIterationAsync(cancellationToken);
                    metrics.Add(iterationMetrics);
                }

                stopwatch.Stop();

                var aggregatedMetrics = AggregateMetrics(metrics);
                return BenchmarkResult.Success(Name, stopwatch.Elapsed, aggregatedMetrics);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return BenchmarkResult.Failure(Name, ex.Message, stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Executes a single iteration without measurement (for warmup).
        /// </summary>
        protected abstract Task ExecuteIterationAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Executes and measures a single iteration.
        /// </summary>
        protected abstract Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Aggregates metrics from multiple iterations.
        /// </summary>
        protected virtual Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var avgThroughput = metrics.Average(m => m.MegabytesPerSecond);
            var minThroughput = metrics.Min(m => m.MegabytesPerSecond);
            var maxThroughput = metrics.Max(m => m.MegabytesPerSecond);
            var avgDuration = TimeSpan.FromMilliseconds(metrics.Average(m => m.Duration.TotalMilliseconds));
            var durationsMs = metrics
                .Select(m => m.Duration.TotalMilliseconds)
                .OrderBy(x => x)
                .ToArray();

            return new Dictionary<string, object>
            {
                ["AvgThroughputMBps"] = avgThroughput,
                ["MinThroughputMBps"] = minThroughput,
                ["MaxThroughputMBps"] = maxThroughput,
                ["AvgDurationMs"] = avgDuration.TotalMilliseconds,
                ["P50DurationMs"] = Percentile(durationsMs, 0.50),
                ["P95DurationMs"] = Percentile(durationsMs, 0.95),
                ["Iterations"] = metrics.Count,
                ["DataSizeBytes"] = _configuration.DataSizeBytes,
                ["DataSize"] = FormatBytes(_configuration.DataSizeBytes)
            };
        }

        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0;
            }

            if (sortedValues.Count == 1)
            {
                return sortedValues[0];
            }

            double index = (sortedValues.Count - 1) * percentile;
            int lowerIndex = (int)Math.Floor(index);
            int upperIndex = (int)Math.Ceiling(index);
            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            double weight = index - lowerIndex;
            return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
        }

        /// <summary>
        /// Formats bytes into human-readable string.
        /// </summary>
        protected static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }

        /// <summary>
        /// Generates test data of specified size.
        /// </summary>
        protected static byte[] GenerateTestData(int size)
        {
            var data = new byte[size];
            Random.Shared.NextBytes(data);
            return data;
        }
    }
}
