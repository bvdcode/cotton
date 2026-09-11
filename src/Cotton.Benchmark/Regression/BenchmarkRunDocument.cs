// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkRunDocument
    {
        public int SchemaVersion { get; init; } = 1;

        public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

        public string Mode { get; init; } = string.Empty;

        public string Profile { get; init; } = string.Empty;

        public string HardwareKey { get; init; } = string.Empty;

        public string GitCommit { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

        public IReadOnlyList<BenchmarkResultSnapshot> Results { get; init; } = [];

        public static BenchmarkRunDocument Create(
            string mode,
            string profile,
            HardwareFingerprint hardwareFingerprint,
            string gitCommit,
            IEnumerable<IBenchmarkResult> results)
        {
            return new BenchmarkRunDocument
            {
                Mode = mode,
                Profile = profile,
                HardwareKey = hardwareFingerprint.Key,
                GitCommit = gitCommit,
                Environment = hardwareFingerprint.Properties,
                Results = results.Select(BenchmarkResultSnapshot.FromResult).ToArray()
            };
        }
    }

}
