// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for memory allocation and stream operations.
    /// </summary>
    public class MemoryStreamBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = GenerateTestData(configuration.DataSizeBytes);

        /// <inheritdoc/>
        public override string Name => "Memory Stream Operations";

        /// <inheritdoc/>
        public override string Description => "Measures memory allocation and stream copy throughput";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_testData);
            await using var outputStream = new MemoryStream();
            await inputStream.CopyToAsync(outputStream, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_testData);
            await using var outputStream = new MemoryStream();
            await inputStream.CopyToAsync(outputStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Operation"] = "Stream Copy";
            return baseMetrics;
        }
    }
}
