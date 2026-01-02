// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    /// <summary>
    /// Defines a contract for performance benchmarks.
    /// </summary>
    public interface IBenchmark
    {
        /// <summary>
        /// Gets the unique name of this benchmark.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of what this benchmark measures.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Executes the benchmark asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The benchmark result containing performance metrics.</returns>
        Task<IBenchmarkResult> RunAsync(CancellationToken cancellationToken = default);
    }
}
