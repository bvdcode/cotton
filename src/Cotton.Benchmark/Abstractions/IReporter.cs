// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    /// <summary>
    /// Defines a contract for reporting benchmark results.
    /// </summary>
    public interface IReporter
    {
        /// <summary>
        /// Reports the results of benchmark executions.
        /// </summary>
        /// <param name="results">The benchmark results to report.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ReportAsync(IEnumerable<IBenchmarkResult> results, CancellationToken cancellationToken = default);
    }
}
