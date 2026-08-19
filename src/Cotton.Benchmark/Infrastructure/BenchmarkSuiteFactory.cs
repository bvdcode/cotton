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
            if (options.Mode != BenchmarkMode.StoragePaths)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unsupported benchmark mode.");
            }

            List<IBenchmark> benchmarks = CreateStoragePathBenchmarks(configuration);
            return ApplyScenarioFilters(benchmarks, options.ScenarioFilters);
        }

        private static List<IBenchmark> CreateStoragePathBenchmarks(BenchmarkConfiguration configuration)
        {
            return
            [
                new HashingBenchmark(configuration),
                new CompressionBenchmark(configuration),
                new DecompressionBenchmark(configuration),
                new EncryptionBenchmark(configuration),
                new DecryptionBenchmark(configuration),
                new FileSystemWriteBenchmark(configuration),
                new FileSystemReadBenchmark(configuration),
                new ChunkUploadProcessingBenchmark(configuration, ChunkUploadDataProfile.MixedContent),
                new PipelineReadBenchmark(configuration)
            ];
        }

        private static List<IBenchmark> ApplyScenarioFilters(IEnumerable<IBenchmark> benchmarks, IReadOnlyList<string> filters)
        {
            List<IBenchmark> benchmarkList = benchmarks.ToList();
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
            return TextMatchesFilter(benchmark.Name, filter);
        }

        private static bool TextMatchesFilter(string value, string filter)
        {
            return value.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || Slugify(value).Contains(Slugify(filter), StringComparison.OrdinalIgnoreCase);
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
