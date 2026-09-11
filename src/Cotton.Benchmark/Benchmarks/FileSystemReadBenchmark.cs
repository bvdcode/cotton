// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Backends;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    public class FileSystemReadBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = TestDataGenerator.GenerateMixedData(configuration.DataSizeBytes);
        private readonly FileSystemStorageBackend _backend = new(NullLogger<FileSystemStorageBackend>.Instance);
        private readonly string _testBasePath = Path.Combine(AppContext.BaseDirectory, "files");

        public override string Name => "Filesystem Backend Read";

        public override string Description => "Measures filesystem backend read throughput";

        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await ReadOnceAsync(measure: false, cancellationToken).ConfigureAwait(false);
        }

        protected override Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            return ReadOnceAsync(measure: true, cancellationToken);
        }

        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Backend"] = "Cotton.Storage.Backends.FileSystemStorageBackend";
            baseMetrics["StoragePath"] = _testBasePath;
            baseMetrics["Operation"] = "Read";
            baseMetrics["DataType"] = "Mixed content";
            return baseMetrics;
        }

        private async Task<PerformanceMetrics> ReadOnceAsync(bool measure, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string uid = CreateUid();
            await using (MemoryStream writeStream = new MemoryStream(_testData, writable: false))
            {
                await _backend.WriteAsync(uid, writeStream).ConfigureAwait(false);
            }

            Stopwatch stopwatch = new();
            try
            {
                if (measure)
                {
                    stopwatch.Start();
                }

                await using Stream readStream = await _backend.ReadAsync(uid).ConfigureAwait(false);
                await using MemoryStream outputStream = new MemoryStream(capacity: _testData.Length);
                await readStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);

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
