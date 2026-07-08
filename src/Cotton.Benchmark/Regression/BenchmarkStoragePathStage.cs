// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkStoragePathStage
    {
        public string Key { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;

        public double MibPerSecond { get; init; }

        public string SourceBenchmark { get; init; } = string.Empty;

        public double? P50DurationMs { get; init; }

        public double? P95DurationMs { get; init; }

        public double? DataSizeBytes { get; init; }
    }
}
