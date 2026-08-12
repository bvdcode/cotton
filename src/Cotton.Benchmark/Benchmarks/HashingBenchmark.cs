// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Benchmark.Benchmarks
{
    public class HashingBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = GenerateTestData(configuration.DataSizeBytes);

        public override string Name => "Hashing (SHA-256)";

        public override string Description => "Measures SHA-256 hashing throughput for content addressing";

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            _ = SHA256.HashData(_testData);
            return Task.CompletedTask;
        }

        protected override Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            _ = SHA256.HashData(_testData);

            stopwatch.Stop();

            return Task.FromResult(PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed));
        }
    }
}
