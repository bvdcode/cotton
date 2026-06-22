// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models
{
    /// <summary>
    /// Categorizes server benchmark measurements.
    /// </summary>
    public enum BenchmarkType
    {
        /// <summary>
        /// Disk throughput benchmark.
        /// </summary>
        DiskSpeed = 1,
        /// <summary>
        /// Disk capacity benchmark.
        /// </summary>
        DiskVolume = 2,
        /// <summary>
        /// CPU throughput benchmark.
        /// </summary>
        ProcessorSpeed = 3,
        /// <summary>
        /// Encryption throughput benchmark.
        /// </summary>
        EncryptionSpeed = 4,
    }
}
