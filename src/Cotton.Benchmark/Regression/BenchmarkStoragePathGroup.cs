// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkStoragePathGroup
    {
        public IReadOnlyList<BenchmarkStoragePathStage> Stages { get; init; } = [];

        public BenchmarkStoragePathStage? Pipeline { get; init; }

        public BenchmarkStoragePathStage? LimitingStage { get; init; }
    }
}
