// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using Cotton.Benchmark.Benchmarks;
using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Benchmark.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cotton.Benchmark
{
    /// <summary>
    /// Entry point for Cotton Cloud performance benchmarking application.
    /// </summary>
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);

            await using var serviceProvider = services.BuildServiceProvider();

            // Print header
            PrintHeader();

            // Print system info
            SystemInfo.PrintSystemInfo();

            // Parse configuration from args
            var configuration = ParseConfiguration(args);
            PrintConfiguration(configuration);

            try
            {
                // Get benchmark runner
                var runner = serviceProvider.GetRequiredService<IBenchmarkRunner>();

                // Create all benchmarks
                var benchmarks = CreateBenchmarks(configuration);

                // Run benchmarks
                var results = await runner.RunBenchmarksAsync(benchmarks);

                // Report results
                var reporter = serviceProvider.GetRequiredService<IReporter>();
                await reporter.ReportAsync(results);

                // Print memory statistics
                PrintMemoryStatistics();

                // Return exit code based on results
                var hasFailures = results.Any(r => !r.IsSuccess);
                return hasFailures ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nFatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Benchmark infrastructure
            services.AddSingleton<IBenchmarkRunner, BenchmarkRunner>();
            services.AddSingleton<IResultFormatter, TableResultFormatter>();
            services.AddSingleton<IReporter, ConsoleReporter>();
        }

        private static List<IBenchmark> CreateBenchmarks(BenchmarkConfiguration configuration)
        {
            return
            [
                new MemoryStreamBenchmark(configuration),
                new HashingBenchmark(configuration),
                new CompressionBenchmark(configuration),
                new DecompressionBenchmark(configuration),
                new MultiSizeCompressionBenchmark(configuration),
                new EncryptionBenchmark(configuration),
                new DecryptionBenchmark(configuration),
                new FileSystemBenchmark(configuration),
                new PipelineBenchmark(configuration)
            ];
        }

        private static BenchmarkConfiguration ParseConfiguration(string[] args)
        {
            // Default configuration
            var config = BenchmarkConfiguration.Default;

            // Parse command line arguments (simple parser)
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--help" || args[i] == "-h")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }
            }

            return config;
        }

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("==================================================================");
            Console.WriteLine("                                                                  ");
            Console.WriteLine("           Cotton Cloud - Performance Benchmark Suite            ");
            Console.WriteLine("                                                                  ");
            Console.WriteLine("  Testing cloud storage pipeline: compression, encryption,       ");
            Console.WriteLine("  hashing, and full cycle performance                            ");
            Console.WriteLine("                                                                  ");
            Console.WriteLine("==================================================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintConfiguration(BenchmarkConfiguration configuration)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  • Data Size:           {FormatBytes(configuration.DataSizeBytes)}");
            Console.WriteLine($"  • Warmup Iterations:   {configuration.WarmupIterations}");
            Console.WriteLine($"  • Measured Iterations: {configuration.MeasuredIterations}");
            Console.WriteLine($"  • Encryption Threads:  {configuration.EncryptionThreads}");
            Console.WriteLine($"  • Cipher Chunk Size:   {FormatBytes(configuration.CipherChunkSizeBytes)}");
            Console.WriteLine($"  • Compression Level:   {configuration.CompressionLevel}");
            Console.WriteLine($"  • Encryption Key Size: {configuration.EncryptionKeySize * 8} bits");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Cotton Cloud Benchmark - Performance Testing Tool");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Cotton.Benchmark [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help     Show this help message");
            Console.WriteLine();
            Console.WriteLine("This tool measures the performance of:");
            Console.WriteLine("  • SHA-256 hashing for content addressing");
            Console.WriteLine("  • Zstd compression and decompression");
            Console.WriteLine("  • AES-GCM encryption and decryption");
            Console.WriteLine("  • Full storage pipeline (compression + encryption)");
        }

        private static string FormatBytes(long bytes)
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

        private static void PrintMemoryStatistics()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Memory Statistics:");
            Console.WriteLine($"  • Current Usage:  {MemoryMonitor.FormatBytes(MemoryMonitor.GetCurrentMemoryUsage())}");
            Console.WriteLine($"  • GC Collections: {MemoryMonitor.GetGCStats()}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
