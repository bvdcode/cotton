// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;

namespace Cotton.Benchmark.Models
{
    public class BenchmarkResult : IBenchmarkResult
    {
        public string BenchmarkName { get; init; } = string.Empty;

        public bool IsSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public TimeSpan TotalDuration { get; init; }

        public IReadOnlyDictionary<string, object> Metrics { get; init; } = new Dictionary<string, object>();

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
