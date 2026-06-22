// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for SHA-256 hashing performance.
    /// </summary>
    public class HashingBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = GenerateTestData(configuration.DataSizeBytes);

        /// <inheritdoc/>
        public override string Name => "Hashing (SHA-256)";

        /// <inheritdoc/>
        public override string Description => "Measures SHA-256 hashing throughput for content addressing";

        /// <inheritdoc/>
        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            _ = SHA256.HashData(_testData);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            _ = SHA256.HashData(_testData);

            stopwatch.Stop();

            return Task.FromResult(PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed));
        }
    }
}
