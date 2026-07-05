// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Models
{
    internal class BenchmarkOptions
    {
        public BenchmarkMode Mode { get; init; } = BenchmarkMode.StoragePaths;

        public BenchmarkProfile Profile { get; init; } = BenchmarkProfile.Standard;

        public bool ShowHelp { get; init; }

        public bool ListBenchmarks { get; init; }

        public bool CompareBaseline { get; init; }

        public bool UpdateBaseline { get; init; }

        public string BaselineDirectory { get; init; } = Path.Combine("performance", "results");

        public string ResultsDirectory { get; init; } = Path.Combine(".temp", "benchmark-results");

        public int? CompressionLevel { get; init; }

        public IReadOnlyList<string> ScenarioFilters { get; init; } = [];
    }
}
