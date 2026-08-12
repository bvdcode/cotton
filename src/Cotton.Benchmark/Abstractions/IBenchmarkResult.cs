// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    public interface IBenchmarkResult
    {
        string BenchmarkName { get; }

        bool IsSuccess { get; }

        string? ErrorMessage { get; }

        TimeSpan TotalDuration { get; }

        IReadOnlyDictionary<string, object> Metrics { get; }
    }
}
