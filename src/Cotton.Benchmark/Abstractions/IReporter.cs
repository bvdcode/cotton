// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Abstractions
{
    public interface IReporter
    {
        Task ReportAsync(IEnumerable<IBenchmarkResult> results, CancellationToken cancellationToken = default);
    }
}
