// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;

namespace Cotton.Benchmark.Models
{
    /// <summary>
    /// Represents the result of a benchmark execution.
    /// </summary>
    public sealed class BenchmarkResult : IBenchmarkResult
    {
        /// <inheritdoc/>
        public string BenchmarkName { get; init; } = string.Empty;

        /// <inheritdoc/>
        public bool IsSuccess { get; init; }

        /// <inheritdoc/>
        public string? ErrorMessage { get; init; }

        /// <inheritdoc/>
        public TimeSpan TotalDuration { get; init; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Metrics { get; init; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a successful benchmark result.
        /// </summary>
        public static BenchmarkResult Success(
            string benchmarkName,
            TimeSpan duration,
            Dictionary<string, object> metrics)
        {
            return new BenchmarkResult
            {
                BenchmarkName = benchmarkName,
                IsSuccess = true,
                TotalDuration = duration,
                Metrics = metrics
            };
        }

        /// <summary>
        /// Creates a failed benchmark result.
        /// </summary>
        public static BenchmarkResult Failure(
            string benchmarkName,
            string errorMessage,
            TimeSpan duration)
        {
            return new BenchmarkResult
            {
                BenchmarkName = benchmarkName,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                TotalDuration = duration,
                Metrics = new Dictionary<string, object>()
            };
        }
    }
}
