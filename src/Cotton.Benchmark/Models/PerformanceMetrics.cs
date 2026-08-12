// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Models
{
    public class PerformanceMetrics
    {
        public long TotalBytes { get; init; }

        public TimeSpan Duration { get; init; }

        public long ManagedAllocatedBytes { get; init; }

        public long WorkingSetBytes { get; init; }

        public long PeakWorkingSetBytes { get; init; }

        public double BytesPerSecond => TotalBytes / Duration.TotalSeconds;

        public double MegabytesPerSecond => BytesPerSecond / (1024 * 1024);

        public double GigabytesPerSecond => BytesPerSecond / (1024 * 1024 * 1024);

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

        public static PerformanceMetrics Create(long totalBytes, TimeSpan duration)
        {
            return new PerformanceMetrics
            {
                TotalBytes = totalBytes,
                Duration = duration
            };
        }

        public PerformanceMetrics WithMemory(
            long managedAllocatedBytes,
            long workingSetBytes,
            long peakWorkingSetBytes)
        {
            return new PerformanceMetrics
            {
                TotalBytes = TotalBytes,
                Duration = Duration,
                ManagedAllocatedBytes = managedAllocatedBytes,
                WorkingSetBytes = workingSetBytes,
                PeakWorkingSetBytes = peakWorkingSetBytes
            };
        }
    }
}
