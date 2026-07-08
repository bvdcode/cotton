// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkStageMapping(string key, string label, string sourceBenchmark)
    {
        public string Key { get; } = key;

        public string Label { get; } = label;

        public string SourceBenchmark { get; } = sourceBenchmark;
    }
}
