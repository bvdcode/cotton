// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    public interface IBenchmarkRunner
    {
        Task<IEnumerable<IBenchmarkResult>> RunBenchmarksAsync(
            IEnumerable<IBenchmark> benchmarks,
            CancellationToken cancellationToken = default);
    }
}
