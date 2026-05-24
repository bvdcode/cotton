// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using Cotton.Storage.Processors;

namespace Cotton.Benchmark.Infrastructure
{
    internal static class BenchmarkConfigurationFactory
    {
        public static BenchmarkConfiguration Create(BenchmarkProfile profile)
        {
            return profile switch
            {
                BenchmarkProfile.Quick => new BenchmarkConfiguration
                {
                    DataSizeBytes = 16 * 1024 * 1024,
                    WarmupIterations = 1,
                    MeasuredIterations = 3,
                    EncryptionThreads = 2,
                    CipherChunkSizeBytes = 1 * 1024 * 1024,
                    CompressionLevel = CompressionProcessor.DefaultCompressionLevel,
                    EncryptionKeySize = 32
                },
                BenchmarkProfile.Standard => BenchmarkConfiguration.Default,
                BenchmarkProfile.Full => new BenchmarkConfiguration
                {
                    DataSizeBytes = 1024 * 1024 * 1024,
                    WarmupIterations = 3,
                    MeasuredIterations = 5,
                    EncryptionThreads = null,
                    CipherChunkSizeBytes = 4 * 1024 * 1024,
                    CompressionLevel = CompressionProcessor.DefaultCompressionLevel,
                    EncryptionKeySize = 32
                },
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported benchmark profile.")
            };
        }
    }
}
