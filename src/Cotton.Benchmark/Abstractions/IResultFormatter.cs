// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    /// <summary>
    /// Defines a contract for formatting benchmark results into human-readable text.
    /// </summary>
    public interface IResultFormatter
    {
        /// <summary>
        /// Formats a single benchmark result.
        /// </summary>
        /// <param name="result">The result to format.</param>
        /// <returns>Formatted text representation.</returns>
        string Format(IBenchmarkResult result);

        /// <summary>
        /// Formats a collection of benchmark results.
        /// </summary>
        /// <param name="results">The results to format.</param>
        /// <returns>Formatted text representation.</returns>
        string FormatCollection(IEnumerable<IBenchmarkResult> results);
    }
}
