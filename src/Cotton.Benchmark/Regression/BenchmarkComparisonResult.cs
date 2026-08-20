// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkComparisonResult
    {
        public bool Passed { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = [];
    }
}
