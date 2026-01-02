// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Processors;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for compression performance using REAL CompressionProcessor from Cotton.Storage.
    /// </summary>
    public sealed class CompressionBenchmark(BenchmarkConfiguration configuration) : BenchmarkBase(configuration)
    {
        private readonly byte[] _testData = TestDataGenerator.GenerateCompressibleText(configuration.DataSizeBytes);
        private readonly CompressionProcessor _processor = new();

        /// <inheritdoc/>
        public override string Name => "Compression (Real Zstd Processor)";

        /// <inheritdoc/>
        public override string Description => $"Tests REAL Cotton.Storage.Processors.CompressionProcessor with compressible text data";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_testData);
            var outputStream = await _processor.WriteAsync("test-uid", inputStream);
            await outputStream.DisposeAsync();
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_testData);
            var outputStream = await _processor.WriteAsync("test-uid", inputStream);
            
            // Read all compressed data to ensure compression is complete
            await using var resultStream = new MemoryStream();
            await outputStream.CopyToAsync(resultStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_testData.Length, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Processor"] = "Cotton.Storage.Processors.CompressionProcessor";
            baseMetrics["DataType"] = "Compressible Text (Logs)";
            return baseMetrics;
        }
    }
}
