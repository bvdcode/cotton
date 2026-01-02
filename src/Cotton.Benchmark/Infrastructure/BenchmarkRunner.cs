// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotton.Benchmark.Infrastructure
{
    /// <summary>
    /// Orchestrates the execution of benchmarks.
    /// </summary>
    public sealed class BenchmarkRunner : IBenchmarkRunner
    {
        private readonly ILogger<BenchmarkRunner> _logger;

        public BenchmarkRunner(ILogger<BenchmarkRunner> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<IBenchmarkResult>> RunBenchmarksAsync(
            IEnumerable<IBenchmark> benchmarks,
            CancellationToken cancellationToken = default)
        {
            var benchmarkList = benchmarks.ToList();
            var results = new List<IBenchmarkResult>();

            _logger.LogInformation("Starting benchmark suite with {Count} benchmarks", benchmarkList.Count);

            foreach (var benchmark in benchmarkList)
            {
                try
                {
                    _logger.LogInformation("Running benchmark: {Name}", benchmark.Name);
                    _logger.LogInformation("Description: {Description}", benchmark.Description);

                    var result = await benchmark.RunAsync(cancellationToken);
                    results.Add(result);

                    if (result.IsSuccess)
                    {
                        _logger.LogInformation("Benchmark '{Name}' completed successfully", benchmark.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Benchmark '{Name}' failed: {Error}", benchmark.Name, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error running benchmark '{Name}'", benchmark.Name);
                    // Create a failure result for unexpected exceptions
                    results.Add(new Models.BenchmarkResult
                    {
                        BenchmarkName = benchmark.Name,
                        IsSuccess = false,
                        ErrorMessage = $"Unexpected error: {ex.Message}",
                        TotalDuration = TimeSpan.Zero,
                        Metrics = new Dictionary<string, object>()
                    });
                }
            }

            _logger.LogInformation("Benchmark suite completed. {Success} successful, {Failed} failed",
                results.Count(r => r.IsSuccess),
                results.Count(r => !r.IsSuccess));

            return results;
        }
    }
}
