// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Processors;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for decompression performance using REAL CompressionProcessor from Cotton.Storage.
    /// </summary>
    public sealed class DecompressionBenchmark : BenchmarkBase
    {
        private readonly byte[] _compressedData;
        private readonly int _originalSize;
        private readonly CompressionProcessor _processor;

        public DecompressionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            // Use REAL CompressionProcessor
            _processor = new CompressionProcessor();

            // Pre-compress REAL compressible data
            var testData = TestDataGenerator.GenerateCompressibleText(configuration.DataSizeBytes);
            _originalSize = testData.Length;

            using var inputStream = new MemoryStream(testData);
            var compressedStream = _processor.WriteAsync("test-uid", inputStream).Result;
            using var outputStream = new MemoryStream();
            compressedStream.CopyTo(outputStream);
            _compressedData = outputStream.ToArray();
        }

        /// <inheritdoc/>
        public override string Name => "Decompression (Real Zstd Processor)";

        /// <inheritdoc/>
        public override string Description => "Tests REAL Cotton.Storage.Processors.CompressionProcessor decompression";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_compressedData);
            var outputStream = await _processor.ReadAsync("test-uid", inputStream);
            await outputStream.DisposeAsync();
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_compressedData);
            var outputStream = await _processor.ReadAsync("test-uid", inputStream);

            // Read all decompressed data
            await using var resultStream = new MemoryStream();
            await outputStream.CopyToAsync(resultStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_originalSize, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Processor"] = "Cotton.Storage.Processors.CompressionProcessor";
            baseMetrics["CompressedSize"] = FormatBytes(_compressedData.Length);
            baseMetrics["CompressionRatio"] = $"{(double)_originalSize / _compressedData.Length:F2}x";
            return baseMetrics;
        }
    }
}
