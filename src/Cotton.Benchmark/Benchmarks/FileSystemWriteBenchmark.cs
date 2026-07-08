// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Backends;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for filesystem backend write throughput.
    /// </summary>
    public class FileSystemWriteBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = TestDataGenerator.GenerateMixedData(configuration.DataSizeBytes);
        private readonly FileSystemStorageBackend _backend = new(NullLogger<FileSystemStorageBackend>.Instance);
        private readonly string _testBasePath = Path.Combine(AppContext.BaseDirectory, "files");

        /// <inheritdoc/>
        public override string Name => "Filesystem Backend Write";

        /// <inheritdoc/>
        public override string Description => "Measures filesystem backend write throughput";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await WriteOnceAsync(measure: false, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            return WriteOnceAsync(measure: true, cancellationToken);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Backend"] = "Cotton.Storage.Backends.FileSystemStorageBackend";
            baseMetrics["StoragePath"] = _testBasePath;
            baseMetrics["Operation"] = "Write";
            baseMetrics["DataType"] = "Mixed content";
            return baseMetrics;
        }

        private async Task<PerformanceMetrics> WriteOnceAsync(bool measure, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string uid = CreateUid();
            Stopwatch stopwatch = new();

            try
            {
                await using var writeStream = new MemoryStream(_testData, writable: false);
                if (measure)
                {
                    stopwatch.Start();
                }

                await _backend.WriteAsync(uid, writeStream).ConfigureAwait(false);

                if (measure)
                {
                    stopwatch.Stop();
                }
            }
            finally
            {
                await _backend.DeleteAsync(uid).ConfigureAwait(false);
            }

            return PerformanceMetrics.Create(
                _testData.Length,
                measure ? stopwatch.Elapsed : TimeSpan.Zero);
        }

        private static string CreateUid()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
