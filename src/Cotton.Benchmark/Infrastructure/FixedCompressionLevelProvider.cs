// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Processors;

namespace Cotton.Benchmark.Infrastructure;

/// <summary>
/// Supplies a deterministic Zstandard level so benchmark profiles measure the configured pipeline.
/// </summary>
internal sealed class FixedCompressionLevelProvider(int level) : ICompressionLevelProvider
{
    /// <inheritdoc />
    public int Level { get; } = level;
}
