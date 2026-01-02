// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Infrastructure
{
    /// <summary>
    /// Utility for measuring memory usage during benchmarks.
    /// </summary>
    public static class MemoryMonitor
    {
        /// <summary>
        /// Gets current memory usage in bytes.
        /// </summary>
        public static long GetCurrentMemoryUsage()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return GC.GetTotalMemory(false);
        }

        /// <summary>
        /// Formats bytes to human-readable string.
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }

        /// <summary>
        /// Gets GC statistics.
        /// </summary>
        public static string GetGCStats()
        {
            return $"Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, Gen2: {GC.CollectionCount(2)}";
        }
    }
}
