// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Regression
{
    internal class HardwareFingerprint
    {
        public string Key { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    }

}
