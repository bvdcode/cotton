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
    /// Benchmark for filesystem backend disk I/O throughput.
    /// </summary>
    public class FileSystemBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = TestDataGenerator.GenerateMixedData(configuration.DataSizeBytes);
        private readonly FileSystemStorageBackend _backend = new(NullLogger<FileSystemStorageBackend>.Instance);
        private readonly string _testBasePath = Path.Combine(AppContext.BaseDirectory, "files");
        private int _uidCounter = 0;

        /// <inheritdoc/>
        public override string Name => "Filesystem Backend I/O";

        /// <inheritdoc/>
        public override string Description => "Measures filesystem backend write/read/delete throughput";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            var uid = GenerateUid();

            // Write to disk
            await using var writeStream = new MemoryStream(_testData);
            await _backend.WriteAsync(uid, writeStream);

            // Read from disk
            await using Stream readStream = await _backend.ReadAsync(uid);
            await using var outputStream = new MemoryStream();
            await readStream.CopyToAsync(outputStream, cancellationToken);

            // Cleanup
            await _backend.DeleteAsync(uid);
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var uid = GenerateUid();
            var stopwatch = Stopwatch.StartNew();

            // Write phase
            await using (var writeStream = new MemoryStream(_testData))
            {
                await _backend.WriteAsync(uid, writeStream);
            }

            // Read phase
            await using (Stream readStream = await _backend.ReadAsync(uid))
            {
                await using var outputStream = new MemoryStream();
                await readStream.CopyToAsync(outputStream, cancellationToken);
            }

            stopwatch.Stop();

            // Cleanup
            await _backend.DeleteAsync(uid);

            // Count both write and read
            return PerformanceMetrics.Create(_testData.Length * 2, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Backend"] = "Cotton.Storage.Backends.FileSystemStorageBackend";
            baseMetrics["StoragePath"] = _testBasePath;
            baseMetrics["Operation"] = "Write + Read + Delete";
            return baseMetrics;
        }

        private string GenerateUid()
        {
            var counter = Interlocked.Increment(ref _uidCounter);
            // Generate hex-only UID (required by StorageKeyHelper)
            return $"{counter:x12}"; // 12 hex digits
        }
    }
}
