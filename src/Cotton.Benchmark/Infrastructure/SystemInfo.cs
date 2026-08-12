// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Runtime.InteropServices;

namespace Cotton.Benchmark.Infrastructure
{
    public static class SystemInfo
    {
        public static string OperatingSystem => RuntimeInformation.OSDescription;

        public static string Framework => RuntimeInformation.FrameworkDescription;

        public static string Architecture => RuntimeInformation.ProcessArchitecture.ToString();

        public static int ProcessorCount => Environment.ProcessorCount;

        public static void PrintSystemInfo()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("System Information:");
            Console.WriteLine($"  OS:           {OperatingSystem}");
            Console.WriteLine($"  Framework:    {Framework}");
            Console.WriteLine($"  Architecture: {Architecture}");
            Console.WriteLine($"  Processors:   {ProcessorCount}");
            Console.WriteLine($"  Memory:       {MemoryMonitor.FormatBytes(Environment.WorkingSet)}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
