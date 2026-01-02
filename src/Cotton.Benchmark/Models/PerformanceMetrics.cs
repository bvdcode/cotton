// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Models
{
    /// <summary>
    /// Represents performance metrics for a benchmark run.
    /// </summary>
    public sealed class PerformanceMetrics
    {
        /// <summary>
        /// Total bytes processed.
        /// </summary>
        public long TotalBytes { get; init; }

        /// <summary>
        /// Total time taken.
        /// </summary>
        public TimeSpan Duration { get; init; }

        /// <summary>
        /// Throughput in bytes per second.
        /// </summary>
        public double BytesPerSecond => TotalBytes / Duration.TotalSeconds;

        /// <summary>
        /// Throughput in megabytes per second.
        /// </summary>
        public double MegabytesPerSecond => BytesPerSecond / (1024 * 1024);

        /// <summary>
        /// Throughput in gigabytes per second.
        /// </summary>
        public double GigabytesPerSecond => BytesPerSecond / (1024 * 1024 * 1024);

        /// <summary>
        /// Gets a human-readable throughput string.
        /// </summary>
        public string ThroughputFormatted
        {
            get
            {
                if (GigabytesPerSecond >= 1.0)
                {
                    return $"{GigabytesPerSecond:F2} GB/s";
                }
                else if (MegabytesPerSecond >= 1.0)
                {
                    return $"{MegabytesPerSecond:F2} MB/s";
                }
                else
                {
                    return $"{BytesPerSecond / 1024:F2} KB/s";
                }
            }
        }

        /// <summary>
        /// Creates metrics from bytes and duration.
        /// </summary>
        public static PerformanceMetrics Create(long totalBytes, TimeSpan duration)
        {
            return new PerformanceMetrics
            {
                TotalBytes = totalBytes,
                Duration = duration
            };
        }
    }
}
