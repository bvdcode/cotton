// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    /// <summary>
    /// Represents the result of a benchmark execution.
    /// </summary>
    public interface IBenchmarkResult
    {
        /// <summary>
        /// Gets the name of the benchmark that produced this result.
        /// </summary>
        string BenchmarkName { get; }

        /// <summary>
        /// Gets whether the benchmark completed successfully.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message if the benchmark failed.
        /// </summary>
        string? ErrorMessage { get; }

        /// <summary>
        /// Gets the total execution time.
        /// </summary>
        TimeSpan TotalDuration { get; }

        /// <summary>
        /// Gets additional metrics specific to this benchmark.
        /// </summary>
        IReadOnlyDictionary<string, object> Metrics { get; }
    }
}
