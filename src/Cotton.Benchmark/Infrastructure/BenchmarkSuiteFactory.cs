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
                BenchmarkMode.Machine => CreateMachineBenchmarks(configuration, options),
                BenchmarkMode.Development => CreateDevelopmentBenchmarks(configuration, options.Profile),
                _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unsupported benchmark mode.")
            };

            return ApplyScenarioFilters(benchmarks, options.ScenarioFilters);
        }

        private static List<IBenchmark> CreateMachineBenchmarks(BenchmarkConfiguration configuration, BenchmarkOptions options)
        {
            List<IBenchmark> benchmarks =
            [
                new MemoryStreamBenchmark(configuration),
                new HashingBenchmark(configuration),
                new ChunkUploadProcessingBenchmark(configuration, ChunkUploadDataProfile.CompressibleText),
                new ChunkUploadProcessingBenchmark(configuration, ChunkUploadDataProfile.MixedContent),
                new ChunkUploadProcessingBenchmark(configuration, ChunkUploadDataProfile.RandomBinary),
                new CompressionBenchmark(configuration),
                new DecompressionBenchmark(configuration),
                new MultiSizeCompressionBenchmark(configuration),
                new EncryptionBenchmark(configuration),
                new DecryptionBenchmark(configuration),
                new FileSystemBenchmark(configuration),
                new PipelineBenchmark(configuration)
            ];

            if (ShouldIncludeExtremeLevelSweep(options.ScenarioFilters))
            {
                benchmarks.Add(new CompressionLevelsBenchmark(configuration));
            }

            return benchmarks;
        }

        private static List<IBenchmark> CreateDevelopmentBenchmarks(BenchmarkConfiguration configuration, BenchmarkProfile profile)
        {
            return
            [
                new FileSystemBenchmark(configuration),
                new ChunkUploadProcessingBenchmark(configuration, ChunkUploadDataProfile.MixedContent),
                new PipelineBenchmark(configuration),
                new ImagePreviewMemoryBenchmark(configuration, profile)
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
            return TextMatchesFilter(benchmark.Name, filter);
        }

        private static bool ShouldIncludeExtremeLevelSweep(IReadOnlyList<string> filters)
        {
            if (filters.Count == 0)
            {
                return false;
            }

            string[] aliases =
            [
                "ZstdSharp Extreme Level Sweep (-5..22)",
                "compression-levels",
                "extreme-level-sweep",
                "zstd-extreme"
            ];

            return filters.Any(filter => aliases.Any(alias => TextMatchesFilter(alias, filter)));
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
