// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using Cotton.Benchmark.Models;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    public abstract class BenchmarkBase(BenchmarkConfiguration configuration) : IBenchmark
    {
        protected readonly BenchmarkConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public abstract string Name { get; }

        public abstract string Description { get; }

        public async Task<IBenchmarkResult> RunAsync(CancellationToken cancellationToken = default)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                // Warmup
                for (int i = 0; i < _configuration.WarmupIterations; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ExecuteIterationAsync(cancellationToken);
                }

                // Actual measurement
                List<PerformanceMetrics> metrics = new List<PerformanceMetrics>();
                for (int i = 0; i < _configuration.MeasuredIterations; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
                    PerformanceMetrics iterationMetrics = await MeasureIterationAsync(cancellationToken);
                    long managedAllocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore);
                    using Process process = Process.GetCurrentProcess();
                    process.Refresh();
                    metrics.Add(iterationMetrics.WithMemory(
                        managedAllocatedBytes,
                        process.WorkingSet64,
                        process.PeakWorkingSet64));
                }

                stopwatch.Stop();

                Dictionary<string, object> aggregatedMetrics = AggregateMetrics(metrics);
                return BenchmarkResult.Success(Name, stopwatch.Elapsed, aggregatedMetrics);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return BenchmarkResult.Failure(Name, ex.Message, stopwatch.Elapsed);
            }
        }

        protected abstract Task ExecuteIterationAsync(CancellationToken cancellationToken);

        protected abstract Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken);

        protected virtual Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            double avgThroughput = metrics.Average(m => m.MegabytesPerSecond);
            double minThroughput = metrics.Min(m => m.MegabytesPerSecond);
            double maxThroughput = metrics.Max(m => m.MegabytesPerSecond);
            TimeSpan avgDuration = TimeSpan.FromMilliseconds(metrics.Average(m => m.Duration.TotalMilliseconds));
            double[] durationsMs = metrics
                .Select(m => m.Duration.TotalMilliseconds)
                .OrderBy(x => x)
                .ToArray();

            Dictionary<string, object> aggregated = new Dictionary<string, object>
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

            AddMemoryMetrics(aggregated, metrics);
            return aggregated;
        }

        protected static void AddMemoryMetrics(
            IDictionary<string, object> target,
            IReadOnlyList<PerformanceMetrics> metrics)
        {
            target["AvgManagedAllocatedBytes"] = metrics.Average(m => m.ManagedAllocatedBytes);
            target["MaxManagedAllocatedBytes"] = metrics.Max(m => m.ManagedAllocatedBytes);
            target["MaxWorkingSetBytes"] = metrics.Max(m => m.WorkingSetBytes);
            target["MaxPeakWorkingSetBytes"] = metrics.Max(m => m.PeakWorkingSetBytes);
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
            return (sortedValues[lowerIndex] * (1 - weight)) + (sortedValues[upperIndex] * weight);
        }

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

        protected static byte[] GenerateTestData(int size)
        {
            byte[] data = new byte[size];
            Random.Shared.NextBytes(data);
            return data;
        }
    }
}
