// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    public interface IBenchmark
    {
        string Name { get; }

        string Description { get; }

        Task<IBenchmarkResult> RunAsync(CancellationToken cancellationToken = default);
    }
}
