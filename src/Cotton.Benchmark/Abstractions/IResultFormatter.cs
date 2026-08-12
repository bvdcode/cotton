// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    public interface IResultFormatter
    {
        string Format(IBenchmarkResult result);

        string FormatCollection(IEnumerable<IBenchmarkResult> results);
    }
}
