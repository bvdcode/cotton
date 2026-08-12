// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Models
{
    public class BenchmarkConfiguration
    {
        public int WarmupIterations { get; init; } = 3;

        public int MeasuredIterations { get; init; } = 10;

        public int DataSizeBytes { get; init; } = 100 * 1024 * 1024; // 100 MB

        public int? EncryptionThreads { get; init; } = 2;

        public int CipherChunkSizeBytes { get; init; } = 1 * 1024 * 1024; // 1 MB

        public int CompressionLevel { get; init; } = Cotton.Storage.Processors.CompressionProcessor.DefaultCompressionLevel;

        public int EncryptionKeySize { get; init; } = 32; // 256-bit

        public static BenchmarkConfiguration Default => new();
    }
}
