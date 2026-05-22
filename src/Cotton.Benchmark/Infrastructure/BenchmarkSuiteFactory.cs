// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using Cotton.Benchmark.Benchmarks;
using Cotton.Benchmark.Models;

namespace Cotton.Benchmark.Infrastructure
{
    internal static class BenchmarkSuiteFactory
    {
        public static List<IBenchmark> Create(BenchmarkConfiguration configuration, BenchmarkOptions options)
        {
            var benchmarks = options.Mode switch
            {
                BenchmarkMode.Machine => CreateMachineBenchmarks(configuration, options.Profile),
                BenchmarkMode.Development => CreateDevelopmentBenchmarks(configuration),
                _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unsupported benchmark mode.")
            };

            return ApplyScenarioFilters(benchmarks, options.ScenarioFilters);
        }

        private static List<IBenchmark> CreateMachineBenchmarks(BenchmarkConfiguration configuration, BenchmarkProfile profile)
        {
            List<IBenchmark> benchmarks =
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

            if (profile != BenchmarkProfile.Quick)
            {
                benchmarks.Add(new CompressionLevelsBenchmark(configuration));
            }

            return benchmarks;
        }

        private static List<IBenchmark> CreateDevelopmentBenchmarks(BenchmarkConfiguration configuration)
        {
            return
            [
                new FileSystemBenchmark(configuration),
                new PipelineBenchmark(configuration)
            ];
        }

        private static List<IBenchmark> ApplyScenarioFilters(IEnumerable<IBenchmark> benchmarks, IReadOnlyList<string> filters)
        {
            var benchmarkList = benchmarks.ToList();
            if (filters.Count == 0)
            {
                return benchmarkList;
            }

            return benchmarkList
                .Where(benchmark => filters.Any(filter => MatchesFilter(benchmark, filter)))
                .ToList();
        }

        private static bool MatchesFilter(IBenchmark benchmark, string filter)
        {
            return benchmark.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || Slugify(benchmark.Name).Contains(Slugify(filter), StringComparison.OrdinalIgnoreCase);
        }

        private static string Slugify(string value)
        {
            return new string(value
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray());
        }
    }
}
