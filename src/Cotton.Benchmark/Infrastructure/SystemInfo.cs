// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Runtime.InteropServices;

namespace Cotton.Benchmark.Infrastructure
{
    /// <summary>
    /// Provides system information for benchmark context.
    /// </summary>
    public static class SystemInfo
    {
        /// <summary>
        /// Gets the operating system description.
        /// </summary>
        public static string OperatingSystem => RuntimeInformation.OSDescription;

        /// <summary>
        /// Gets the runtime framework description.
        /// </summary>
        public static string Framework => RuntimeInformation.FrameworkDescription;

        /// <summary>
        /// Gets the processor architecture.
        /// </summary>
        public static string Architecture => RuntimeInformation.ProcessArchitecture.ToString();

        /// <summary>
        /// Gets the number of logical processors.
        /// </summary>
        public static int ProcessorCount => Environment.ProcessorCount;

        /// <summary>
        /// Prints system information to console.
        /// </summary>
        public static void PrintSystemInfo()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("System Information:");
            Console.WriteLine($"  • OS:          {OperatingSystem}");
            Console.WriteLine($"  • Framework:   {Framework}");
            Console.WriteLine($"  • Architecture: {Architecture}");
            Console.WriteLine($"  • Processors:  {ProcessorCount}");
            Console.WriteLine($"  • Memory:      {MemoryMonitor.FormatBytes(Environment.WorkingSet)}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
