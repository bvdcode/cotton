// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    /// <summary>
    /// Defines a contract for running and orchestrating benchmarks.
    /// </summary>
    public interface IBenchmarkRunner
    {
        /// <summary>
        /// Runs a collection of benchmarks and returns their results.
        /// </summary>
        /// <param name="benchmarks">The benchmarks to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A collection of benchmark results.</returns>
        Task<IEnumerable<IBenchmarkResult>> RunBenchmarksAsync(
            IEnumerable<IBenchmark> benchmarks,
            CancellationToken cancellationToken = default);
    }
}
