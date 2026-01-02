// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Models
{
    /// <summary>
    /// Configuration for benchmark execution.
    /// </summary>
    public sealed class BenchmarkConfiguration
    {
        /// <summary>
        /// Number of warmup iterations before actual measurement.
        /// </summary>
        public int WarmupIterations { get; init; } = 2;

        /// <summary>
        /// Number of measured iterations.
        /// </summary>
        public int MeasuredIterations { get; init; } = 5;

        /// <summary>
        /// Size of data to use for benchmarks (in bytes).
        /// </summary>
        public int DataSizeBytes { get; init; } = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// Number of encryption threads to use.
        /// </summary>
        public int EncryptionThreads { get; init; } = 2;

        /// <summary>
        /// Size of cipher chunks (in bytes).
        /// </summary>
        public int CipherChunkSizeBytes { get; init; } = 1 * 1024 * 1024; // 1 MB

        /// <summary>
        /// Compression level (1-22 for Zstd).
        /// </summary>
        public int CompressionLevel { get; init; } = 3;

        /// <summary>
        /// Gets the default configuration.
        /// </summary>
        public static BenchmarkConfiguration Default => new();
    }
}
