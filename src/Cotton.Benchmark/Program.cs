// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Benchmark.Regression;
using Cotton.Benchmark.Reporting;
using Cotton.Storage.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Cotton.Benchmark
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            BenchmarkOptions options;
            try
            {
                options = BenchmarkOptionParser.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintHelp();
                return 2;
            }

            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            BenchmarkConfiguration configuration = ApplyConfigurationOverrides(
                BenchmarkConfigurationFactory.Create(options.Profile),
                options);
            HardwareFingerprint hardwareFingerprint = new HardwareFingerprintProvider().Create();
            List<IBenchmark> benchmarks = BenchmarkSuiteFactory.Create(configuration, options);

            await using ServiceProvider serviceProvider = CreateServiceProvider();

            PrintHeader();
            SystemInfo.PrintSystemInfo();
            PrintBenchmarkContext(options, configuration, hardwareFingerprint);

            if (options.ListBenchmarks)
            {
                PrintBenchmarkList(benchmarks);
                return 0;
            }

            if (benchmarks.Count == 0)
            {
                Console.Error.WriteLine("No benchmarks matched the requested mode and scenario filters.");
                return 2;
            }

            return await RunBenchmarksAsync(serviceProvider, benchmarks, options, hardwareFingerprint);
        }

        private static BenchmarkConfiguration ApplyConfigurationOverrides(
            BenchmarkConfiguration configuration,
            BenchmarkOptions options)
        {
            if (!options.CompressionLevel.HasValue)
            {
                return configuration;
            }

            int level = options.CompressionLevel.Value;
            CompressionProcessor.ThrowIfInvalidLevel(level);
            return new BenchmarkConfiguration
            {
                WarmupIterations = configuration.WarmupIterations,
                MeasuredIterations = configuration.MeasuredIterations,
                DataSizeBytes = configuration.DataSizeBytes,
                EncryptionThreads = configuration.EncryptionThreads,
                CipherChunkSizeBytes = configuration.CipherChunkSizeBytes,
                CompressionLevel = level,
                EncryptionKeySize = configuration.EncryptionKeySize
            };
        }

        private static ServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddSingleton<IBenchmarkRunner, BenchmarkRunner>();
            services.AddSingleton<IResultFormatter, TableResultFormatter>();
            services.AddSingleton<IReporter, ConsoleReporter>();

            return services.BuildServiceProvider();
        }

        private static async Task<int> RunBenchmarksAsync(
            ServiceProvider serviceProvider,
            IReadOnlyList<IBenchmark> benchmarks,
            BenchmarkOptions options,
            HardwareFingerprint hardwareFingerprint)
        {
            try
            {
                IBenchmarkRunner runner = serviceProvider.GetRequiredService<IBenchmarkRunner>();
                IBenchmarkResult[] results = (await runner.RunBenchmarksAsync(benchmarks)).ToArray();

                IReporter reporter = serviceProvider.GetRequiredService<IReporter>();
                await reporter.ReportAsync(results);

                PrintMemoryStatistics();

                var runDocument = BenchmarkRunDocument.Create(
                    FormatEnum(options.Mode),
                    FormatEnum(options.Profile),
                    hardwareFingerprint,
                    new GitRevisionProvider().GetCurrentRevision(),
                    results);

                int comparisonExitCode = await SaveAndCompareAsync(options, runDocument);
                if (comparisonExitCode != 0)
                {
                    return comparisonExitCode;
                }

                return results.Any(r => !r.IsSuccess) ? 1 : 0;
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

        private static async Task<int> SaveAndCompareAsync(BenchmarkOptions options, BenchmarkRunDocument runDocument)
        {
            var artifactStore = new BenchmarkArtifactStore(options.BaselineDirectory, options.ResultsDirectory);
            BenchmarkStoragePathSummaryDocument storagePathSummary = BenchmarkStoragePathSummaryDocument.Create(runDocument);

            if (options.UpdateBaseline)
            {
                string baselineSummaryPath = await artifactStore.SaveBaselineSummaryAsync(
                    storagePathSummary,
                    CancellationToken.None);
                Console.WriteLine($"Updated benchmark result: {baselineSummaryPath}");
            }
            else
            {
                string resultPath = await artifactStore.SaveResultAsync(runDocument, CancellationToken.None);
                Console.WriteLine($"Saved scratch benchmark result: {resultPath}");
                string resultSummaryPath = await artifactStore.SaveResultSummaryAsync(
                    resultPath,
                    storagePathSummary,
                    CancellationToken.None);
                Console.WriteLine($"Saved scratch storage path result: {resultSummaryPath}");
            }

            if (!options.CompareBaseline)
            {
                return 0;
            }

            BenchmarkRunDocument? baseline = await artifactStore.LoadBaselineAsync(runDocument, CancellationToken.None);
            if (baseline is null)
            {
                Console.Error.WriteLine($"No reviewed result found: {artifactStore.GetBaselinePath(runDocument)}");
                Console.Error.WriteLine("Run again with --update-baseline after reviewing the result.");
                return 2;
            }

            BenchmarkComparisonResult comparison = new BenchmarkRegressionComparer().Compare(baseline, runDocument);
            PrintComparison(comparison);
            return comparison.Passed ? 0 : 1;
        }

        private static string FormatEnum<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            string text = value.ToString();
            return string.Concat(text.Select((character, index) =>
            {
                if (index > 0 && char.IsUpper(character))
                {
                    return $"-{char.ToLowerInvariant(character)}";
                }

                return char.ToLowerInvariant(character).ToString();
            }));
        }

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("==================================================================");
            Console.WriteLine("                                                                  ");
            Console.WriteLine("           Cotton Cloud - Performance Benchmark Suite            ");
            Console.WriteLine("                                                                  ");
            Console.WriteLine("  Local storage-path benchmarks and reviewed results              ");
            Console.WriteLine("                                                                  ");
            Console.WriteLine("==================================================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintBenchmarkContext(
            BenchmarkOptions options,
            BenchmarkConfiguration configuration,
            HardwareFingerprint hardwareFingerprint)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Benchmark Context:");
            Console.WriteLine($"  • Mode:                {FormatEnum(options.Mode)}");
            Console.WriteLine($"  • Profile:             {FormatEnum(options.Profile)}");
            Console.WriteLine($"  • Hardware Key:        {hardwareFingerprint.Key}");
            Console.WriteLine($"  • Results Directory:   {options.BaselineDirectory}");
            Console.WriteLine($"  • Scratch Directory:   {options.ResultsDirectory}");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  • Data Size:           {FormatBytes(configuration.DataSizeBytes)}");
            Console.WriteLine($"  • Warmup Iterations:   {configuration.WarmupIterations}");
            Console.WriteLine($"  • Measured Iterations: {configuration.MeasuredIterations}");
            Console.WriteLine($"  • Encryption Threads:  {configuration.EncryptionThreads?.ToString(CultureInfo.InvariantCulture) ?? "auto"}");
            Console.WriteLine($"  • Cipher Chunk Size:   {FormatBytes(configuration.CipherChunkSizeBytes)}");
            Console.WriteLine($"  • Compression Level:   {configuration.CompressionLevel}");
            Console.WriteLine($"  • Encryption Key Size: {configuration.EncryptionKeySize * 8} bits");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintBenchmarkList(IEnumerable<IBenchmark> benchmarks)
        {
            Console.WriteLine("Available benchmarks:");
            foreach (IBenchmark benchmark in benchmarks)
            {
                Console.WriteLine($"  - {benchmark.Name}: {benchmark.Description}");
            }
        }

        private static void PrintComparison(BenchmarkComparisonResult comparison)
        {
            Console.ForegroundColor = comparison.Passed ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(comparison.Passed ? "Performance comparison passed." : "Performance comparison failed.");
            Console.ResetColor();

            foreach (string message in comparison.Messages)
            {
                Console.WriteLine($"  - {message}");
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Cotton Cloud Benchmark - Performance Testing Tool");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project src/Cotton.Benchmark -c Release -- [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help              Show this help message");
            Console.WriteLine("  --mode <value>          storage-paths");
            Console.WriteLine("  --profile <value>       quick | standard | full");
            Console.WriteLine("  --scenario <filter>     Run only matching benchmark names; can be comma-separated");
            Console.WriteLine("  --compression-level <n> Override Zstd level for configured pipeline benchmarks");
            Console.WriteLine("  --list                  List benchmarks for the selected mode");
            Console.WriteLine("  --compare               Compare with the committed result for this hardware key");
            Console.WriteLine("  --update-baseline       Save this run as the reviewed result; default for full non-compare runs");
            Console.WriteLine("  --no-update-baseline    Save only a scratch result");
            Console.WriteLine("  --baseline-dir <path>   Reviewed result directory; default <repo>/performance/results");
            Console.WriteLine("  --results-dir <path>    Scratch result directory; default <repo>/.temp/benchmark-results");
            Console.WriteLine();
            Console.WriteLine("Modes:");
            Console.WriteLine("  storage-paths Public write/read storage-path benchmarks used for published results.");
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
